using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;
using Workflows.Tools.Processes;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.NuGet;

[TestClass]
public sealed class PublishCommandTests
{
    private const string Pushed = "Pushing...\nYour package was pushed.\n";

    [TestMethod]
    [DataRow(0, "Your package was pushed.\n", "uploaded")]
    [DataRow(0, "  Created https://example.test/api/v2/package/\n", "uploaded")]
    [DataRow(0, "warn : 409 Conflict: package already exists\n", "duplicate")]
    [DataRow(0, "already exists\nYour package was pushed.\n", "duplicate")]
    [DataRow(0, "Push finished\n", "unconfirmed")]
    [DataRow(1, "Your package was pushed.\n", "failed")]
    public void Classify_RequiresConfirmedUpload(int exitCode, string output, string expected)
    {
        Assert.AreEqual(expected, PushOutput.Classify(exitCode, output).ToDisplayString());
    }

    [TestMethod]
    public async Task Run_OnlyConfirmedUploadsAreNewlyUploaded()
    {
        using var runner = new TestRunner();
        runner.Environment["NUGET_API_KEY"] = "nuget-key";
        runner.Environment["GITHUB_PACKAGES_TOKEN"] = "github-token";
        runner.State.Manifest = [runner.Entry("New", "1.0.0"), runner.Entry("Duplicate", "1.0.0"), runner.Entry("Partial", "1.0.0")];

        var processes = new FakeProcessRunner((_, arguments) =>
        {
            var package = Path.GetFileName(arguments[2]);
            var feed = arguments[6];

            return package switch
            {
                _ when package.StartsWith("New") => FakeProcessRunner.Success(feed.Contains("nuget.org") ? Pushed : "409 Conflict"),
                _ when package.StartsWith("Partial") => feed.Contains("github") ? FakeProcessRunner.Success(Pushed) : FakeProcessRunner.Failure("error"),
                _ => FakeProcessRunner.Success("already exists"),
            };
        });

        await new PublishCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(new[] { "New", "Partial" }, runner.State.NewlyUploaded.Select(package => package.Id).ToList());
        StringAssert.Contains(runner.Output, "has-new=true");
        StringAssert.Contains(runner.Output, "has-failures=true");
    }

    [TestMethod]
    public async Task Run_SymbolsOnlyUploadIsNotNewlyUploaded()
    {
        using var runner = new TestRunner();
        runner.Environment["PUSH_TO_GITHUB"] = "false";
        runner.Environment["NUGET_API_KEY"] = "nuget-key";
        runner.WritePackage("Lib", "1.0.0", withSymbols: true);
        runner.State.Manifest = [runner.Entry("Lib", "1.0.0")];

        var processes = new FakeProcessRunner((_, arguments) => arguments[2].EndsWith(".snupkg")
            ? FakeProcessRunner.Success(Pushed)
            : FakeProcessRunner.Success("409 Conflict"));

        await new PublishCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual(0, runner.State.NewlyUploaded.Count);
        StringAssert.Contains(runner.Output, "has-new=false");
        StringAssert.Contains(runner.Output, "has-failures=false");
    }

    [TestMethod]
    public async Task Run_PushesPackageWithoutSymbolsAndSymbolsOnlyToNuGetOrg()
    {
        using var runner = new TestRunner();
        runner.Environment["NUGET_API_KEY"] = "nuget-key";
        runner.Environment["GITHUB_PACKAGES_TOKEN"] = "github-token";
        var package = runner.WritePackage("Lib", "1.0.0", withSymbols: true);
        runner.State.Manifest = [runner.Entry("Lib", "1.0.0")];

        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(Pushed));
        await new PublishCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                $"dotnet nuget push {package} --api-key nuget-key --source https://api.nuget.org/v3/index.json --skip-duplicate --force-english-output --no-symbols",
                $"dotnet nuget push {package} --api-key github-token --source https://nuget.pkg.github.com/owner/index.json --skip-duplicate --force-english-output --no-symbols",
                $"dotnet nuget push {Path.ChangeExtension(package, ".snupkg")} --api-key nuget-key --source https://api.nuget.org/v3/index.json --skip-duplicate --force-english-output",
            },
            processes.CommandLines.ToList());
    }

    [TestMethod]
    public async Task Run_MissingApiKeyFailsWithoutPushing()
    {
        using var runner = new TestRunner();
        runner.Environment["PUSH_TO_GITHUB"] = "false";
        runner.State.Manifest = [runner.Entry("Lib", "1.0.0")];

        var processes = new FakeProcessRunner();
        await new PublishCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual(0, processes.Calls.Count);
        StringAssert.Contains(runner.Log.ToString(), "Lib.1.0.0.nupkg -> NuGet.org: missing API key");
        StringAssert.Contains(runner.Output, "has-failures=true");
    }

    [TestMethod]
    public async Task Run_RedactsApiKeyFromFailureOutput()
    {
        using var runner = new TestRunner();
        runner.Environment["PUSH_TO_GITHUB"] = "false";
        runner.Environment["NUGET_API_KEY"] = "secret-key";
        runner.State.Manifest = [runner.Entry("Lib", "1.0.0")];

        await new PublishCommand(runner.Context, new FakeProcessRunner((_, _) => FakeProcessRunner.Failure("bad key secret-key"))).RunAsync();

        StringAssert.Contains(runner.Log.ToString(), "bad key [redacted]");
        Assert.IsFalse(runner.Log.ToString().Contains("secret-key"));
    }

    [TestMethod]
    public async Task Run_WritesSummaryTable()
    {
        using var runner = new TestRunner();
        runner.Environment["NUGET_API_KEY"] = "nuget-key";
        runner.Environment["GITHUB_PACKAGES_TOKEN"] = "github-token";
        runner.WritePackage("A", "1.0.0", withSymbols: true);
        runner.State.Manifest = [runner.Entry("A", "1.0.0"), runner.Entry("B", "2.0.0")];

        var processes = new FakeProcessRunner((_, arguments) => arguments[6].Contains("github")
            ? FakeProcessRunner.Success("409 Conflict")
            : FakeProcessRunner.Success(Pushed));

        await new PublishCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual(
            """
            ## NuGet publishing

            | Package | NuGet.org | GitHub Packages | Symbols |
            | --- | --- | --- | --- |
            | A 1.0.0 | uploaded | duplicate | uploaded |
            | B 2.0.0 | uploaded | duplicate | none |

            Newly uploaded packages: 2.

            """.ReplaceLineEndings("\n"),
            runner.Summary);
    }

    [TestMethod]
    public void ConfiguredFeeds_HonorSelectedDestinations()
    {
        using var runner = new TestRunner();
        runner.Environment["PUSH_TO_NUGET"] = "false";
        runner.Environment["PUSH_TO_GITHUB"] = "false";
        runner.Environment["PUSH_TO_CUSTOM_FEEDS"] = "true";
        runner.Environment["CUSTOM_FEED_URLS"] = "https://one.example/v3/index.json\nhttps://two.example/v3/index.json";
        runner.Environment["CUSTOM_FEED_API_KEYS"] = "one-key\ntwo-key";

        var feeds = new PublishCommand(runner.Context, new FakeProcessRunner()).ConfiguredFeeds();

        CollectionAssert.AreEqual(
            new[]
            {
                new Feed("Custom feed 1", "https://one.example/v3/index.json", "one-key"),
                new Feed("Custom feed 2", "https://two.example/v3/index.json", "two-key"),
            },
            feeds.ToList());
    }

    [TestMethod]
    public void ConfiguredFeeds_ResolvesRelativeFolderFromRepositoryRoot()
    {
        using var runner = new TestRunner();
        runner.Environment["PUSH_TO_NUGET"] = "false";
        runner.Environment["PUSH_TO_GITHUB"] = "false";
        runner.Environment["PUSH_TO_CUSTOM_FEEDS"] = "true";
        runner.Environment["CUSTOM_FEED_URLS"] = "local/feed";

        var feed = new PublishCommand(runner.Context, new FakeProcessRunner()).ConfiguredFeeds().Single();

        Assert.AreEqual(Path.Combine(runner.Workspace, "local", "feed"), feed.Url);
    }

    [TestMethod]
    public void ConfiguredFeeds_CustomFeedsRequireUrl()
    {
        using var runner = new TestRunner();
        runner.Environment["PUSH_TO_CUSTOM_FEEDS"] = "true";

        var exception = Assert.Throws<WorkflowException>(() => new PublishCommand(runner.Context, new FakeProcessRunner()).ConfiguredFeeds());

        Assert.AreEqual("push-to-custom-feeds requires at least one custom-feed-url", exception.Message);
    }
}
