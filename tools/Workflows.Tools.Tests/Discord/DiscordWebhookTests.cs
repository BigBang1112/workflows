using System.Net;
using Workflows.Tools.Discord;
using Workflows.Tools.GitHub;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.Discord;

[TestClass]
public sealed class DiscordWebhookTests
{
    [TestMethod]
    public void Split_RespectsUtf16Limit()
    {
        var message = string.Concat(Enumerable.Repeat(string.Concat(Enumerable.Repeat("\U0001F600", 400)) + "\n", 5));

        var chunks = DiscordWebhook.Split(message).ToList();

        Assert.IsGreaterThan(1, chunks.Count);
        Assert.AreEqual(message, string.Concat(chunks));
        Assert.IsTrue(chunks.All(chunk => chunk.EndsWith('\n')));
        Assert.IsTrue(chunks.All(chunk => chunk.Length <= DiscordWebhook.MessageLimit));
    }

    [TestMethod]
    public void Split_NeverSplitsLongLine()
    {
        var exception = Assert.Throws<WorkflowException>(() => DiscordWebhook.Split(new string('a', 1901)).ToList());

        StringAssert.Contains(exception.Message, "cannot be split at a line break");
    }

    [TestMethod]
    public async Task Post_RetriesAfterRateLimit()
    {
        var delays = new List<TimeSpan>();
        var handler = new FakeHttpHandler((_, attempt) => attempt < 3
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new(TimeSpan.FromSeconds(2)) } }
            : new HttpResponseMessage(HttpStatusCode.NoContent));

        await new DiscordWebhook(new HttpClient(handler), delay => { delays.Add(delay); return Task.CompletedTask; })
            .PostAsync("https://discord.example/webhook", "Hello\n");

        Assert.AreEqual(3, handler.Requests.Count);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) }, delays);
    }

    [TestMethod]
    public async Task Post_GivesUpAfterFourRateLimits()
    {
        var handler = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new DiscordWebhook(new HttpClient(handler), _ => Task.CompletedTask).PostAsync("https://discord.example/webhook", "Hello\n"));

        Assert.AreEqual(4, handler.Requests.Count);
        Assert.AreEqual("Discord webhook request failed with HTTP 429", exception.Message);
    }
}
