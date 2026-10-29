using System.Net;
using System.Text.Json;
using Workflows.Tools.Apps;
using Workflows.Tools.GitHub;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.Apps;

[TestClass]
public sealed class ReleaseAppCommandTests
{
    private const string ReleaseUrl = "https://github.com/owner/repo/releases/tag/v1.0.0";
    private const string Footer = "Assets were automatically generated using the [publish workflow](<https://github.com/owner/repo/actions/runs/123>).\n";

    private static List<string> WriteAssets(TestRunner runner, params string[] names)
    {
        var assets = new AppPaths(runner.Context).Assets;
        Directory.CreateDirectory(assets);
        return names.Select(name =>
        {
            var path = Path.Combine(assets, name);
            File.WriteAllText(path, "");
            return path;
        }).ToList();
    }

    private static ReleaseAppCommand Command(TestRunner runner, FakeProcessRunner processes, FakeHttpHandler? http = null)
    {
        return new ReleaseAppCommand(runner.Context, processes, new GitHubApi(new HttpClient(http ?? new FakeHttpHandler()), "https://api.github.test", "token"));
    }

    [TestMethod]
    public async Task Run_PushedTagCreatesReleaseWithAssetsAndVerifiesTag()
    {
        using var runner = new TestRunner();
        runner.Environment["GITHUB_REF_TYPE"] = "tag";
        runner.Environment["GITHUB_REF_NAME"] = "v1.0.0";
        runner.Environment["RELEASE_TITLE_PREFIX"] = "App";
        runner.Environment["RELEASE_DESCRIPTION"] = "  First release.\n";
        var assets = WriteAssets(runner, "App-windows-x64.zip", "App-linux-x64.zip");

        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(ReleaseUrl + "\n"));
        await Command(runner, processes).RunAsync();

        var notesPath = new AppPaths(runner.Context).ReleaseNotes;
        CollectionAssert.AreEqual(
            new[]
            {
                "release", "create", "v1.0.0", assets[1], assets[0],
                "--title", "App 1.0.0", "--notes-file", notesPath,
                "--target", "abc123", "--repo", "owner/repo", "--verify-tag",
            },
            processes.Calls.Single().Arguments.ToList());
        Assert.AreEqual("First release.\n\n" + Footer, File.ReadAllText(notesPath));
        StringAssert.Contains(runner.Output, $"url={ReleaseUrl}");
    }

    [TestMethod]
    public async Task Run_TagInputCreatesTagWithGeneratedNotes()
    {
        using var runner = new TestRunner();
        runner.Environment["GITHUB_REF_TYPE"] = "branch";
        runner.Environment["GITHUB_REF_NAME"] = "main";
        runner.Environment["TAG"] = "v2.0.0";
        runner.Environment["RELEASE_DESCRIPTION"] = "Intro";
        runner.Environment["GENERATE_RELEASE_NOTES"] = "true";
        WriteAssets(runner, "App.zip");

        var http = new FakeHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"name":"v2.0.0","body":"## What's Changed\n* Fix by @someone\n"}"""),
        });
        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(ReleaseUrl));
        await Command(runner, processes, http).RunAsync();

        var (request, body) = http.Requests.Single();
        Assert.AreEqual("/repos/owner/repo/releases/generate-notes", request.RequestUri!.AbsolutePath);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.AreEqual("v2.0.0", json.GetProperty("tag_name").GetString());
        Assert.AreEqual("abc123", json.GetProperty("target_commitish").GetString());

        Assert.AreEqual("Intro\n\n## What's Changed\n* Fix by @someone\n\n" + Footer, File.ReadAllText(new AppPaths(runner.Context).ReleaseNotes));
        var arguments = processes.Calls.Single().Arguments.ToList();
        CollectionAssert.AreEqual(new[] { "release", "create", "v2.0.0" }, arguments.Take(3).ToList());
        CollectionAssert.DoesNotContain(arguments, "--verify-tag");
    }

    [TestMethod]
    public async Task Run_FailsWithoutTag()
    {
        using var runner = new TestRunner();
        runner.Environment["GITHUB_REF_TYPE"] = "branch";
        WriteAssets(runner, "App.zip");

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => Command(runner, new FakeProcessRunner()).RunAsync());

        Assert.AreEqual("Set the tag input, or run the workflow for a pushed tag", exception.Message);
    }

    [TestMethod]
    public async Task Run_FailsWithoutAssets()
    {
        using var runner = new TestRunner();
        runner.Environment["TAG"] = "v1.0.0";
        var processes = new FakeProcessRunner();

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => Command(runner, processes).RunAsync());

        StringAssert.StartsWith(exception.Message, "No assets found in ");
        Assert.AreEqual(0, processes.Calls.Count);
    }

    [TestMethod]
    public async Task Run_FailsWithGitHubError()
    {
        using var runner = new TestRunner();
        runner.Environment["TAG"] = "v1.0.0";
        WriteAssets(runner, "App.zip");

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            Command(runner, new FakeProcessRunner((_, _) => FakeProcessRunner.Failure("HTTP 422: Release.tag_name already exists"))).RunAsync());

        Assert.AreEqual("GitHub release creation failed: HTTP 422: Release.tag_name already exists", exception.Message);
    }
}
