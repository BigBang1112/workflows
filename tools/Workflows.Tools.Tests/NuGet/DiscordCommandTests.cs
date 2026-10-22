using System.Net;
using System.Text.Json;
using Workflows.Tools.Discord;
using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.NuGet;

[TestClass]
public sealed class DiscordCommandTests
{
    private const string WebhookUrl = "https://discord.example/private-webhook";

    private static TestRunner CreateRunner(string notes, PushStatus nugetStatus = PushStatus.Uploaded)
    {
        var runner = new TestRunner();
        runner.Environment["DISCORD_WEBHOOK_URL"] = WebhookUrl;
        runner.Environment["RELEASE_URL"] = "https://github.com/owner/repo/releases/tag/v2.4.4";
        runner.State.NewlyUploaded = [runner.Entry("GBX.NET", "2.4.4", notes)];
        runner.State.Results = [new PublishResult("GBX.NET", "2.4.4", new() { [PublishCommand.NuGetOrg] = nugetStatus })];
        return runner;
    }

    private static string Content(string body) => JsonDocument.Parse(body).RootElement.GetProperty("content").GetString()!;

    [TestMethod]
    public async Task Run_PostsSeveralMessagesWithoutExtraText()
    {
        using var runner = CreateRunner(string.Join('\n', Enumerable.Repeat("- Changed a thing", 200)));
        var handler = new FakeHttpHandler();

        await new DiscordCommand(runner.Context, new DiscordWebhook(new HttpClient(handler))).RunAsync();

        var contents = handler.Requests.Select(request => Content(request.Body)).ToList();
        Assert.IsGreaterThan(1, contents.Count);
        Assert.IsTrue(contents.All(content => content.Length <= DiscordWebhook.MessageLimit));
        Assert.IsTrue(handler.Requests.All(request => request.Request.Headers.UserAgent.Count == 0));
        Assert.IsTrue(contents[0].StartsWith("## GBX.NET 2.4.4\n\n- Changed a thing\n"));
        StringAssert.EndsWith(contents[^1], "NuGet: <https://www.nuget.org/packages/GBX.NET/2.4.4>\n");
    }

    [TestMethod]
    public async Task Run_LeavesOutNuGetLinkWhenMainPackageIsNotOnNuGetOrg()
    {
        using var runner = CreateRunner("- Fixed", PushStatus.Failed);
        var handler = new FakeHttpHandler();

        await new DiscordCommand(runner.Context, new DiscordWebhook(new HttpClient(handler))).RunAsync();

        Assert.IsFalse(Content(handler.Requests.Single().Body).Contains("NuGet:"));
    }

    [TestMethod]
    public async Task Run_ReportsDiscordErrorWithoutWebhookUrl()
    {
        using var runner = CreateRunner("- Changed a thing");
        var handler = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"message":"Missing Access"}"""),
        });

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new DiscordCommand(runner.Context, new DiscordWebhook(new HttpClient(handler))).RunAsync());

        Assert.AreEqual("Discord webhook request failed with HTTP 403: Missing Access", exception.Message);
        Assert.IsFalse(exception.Message.Contains("private-webhook"));
    }

    [TestMethod]
    public async Task Run_RejectsOverlongLineBeforePosting()
    {
        using var runner = CreateRunner(string.Concat(Enumerable.Repeat("- Short\n", 200)) + new string('x', 1901));
        var handler = new FakeHttpHandler();

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new DiscordCommand(runner.Context, new DiscordWebhook(new HttpClient(handler))).RunAsync());

        StringAssert.Contains(exception.Message, "cannot be split at a line break");
        Assert.AreEqual(0, handler.Requests.Count);
    }
}
