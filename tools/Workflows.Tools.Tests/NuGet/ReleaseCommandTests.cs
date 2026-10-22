using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.NuGet;

[TestClass]
public sealed class ReleaseCommandTests
{
    private const string ReleaseUrl = "https://github.com/owner/repo/releases/tag/v1.0.0";

    [TestMethod]
    public async Task Run_AttachesOnlyNewlyUploadedPackagesAndVerifiesTag()
    {
        using var runner = new TestRunner();
        runner.Environment["GITHUB_REF_TYPE"] = "tag";
        runner.Environment["GITHUB_REF_NAME"] = "v1.0.0";
        var package = runner.WritePackage("New", "1.0.0", withSymbols: true);
        runner.WritePackage("Duplicate", "1.0.0");
        runner.State.NewlyUploaded = [runner.Entry("New", "1.0.0", "New notes")];

        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(ReleaseUrl + "\n"));
        await new ReleaseCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                "release", "create", "v1.0.0", package, Path.ChangeExtension(package, ".snupkg"),
                "--title", "1.0.0", "--notes-file", runner.State.ReleaseNotesPath,
                "--target", "abc123", "--repo", "owner/repo", "--verify-tag",
            },
            processes.Calls.Single().Arguments.ToList());
        StringAssert.Contains(runner.Output, "created=true");
        StringAssert.Contains(runner.Output, $"url={ReleaseUrl}");
    }

    [TestMethod]
    public async Task Run_WithoutTagUsesMainPackageVersion()
    {
        using var runner = new TestRunner();
        runner.Environment["GITHUB_REF_TYPE"] = "branch";
        runner.Environment["MAIN_PROJECT"] = "Main";
        runner.Environment["UPLOAD_TO_RELEASE"] = "false";
        runner.State.NewlyUploaded = [runner.Entry("Main", "1.2.0"), runner.Entry("Other", "3.0.0")];

        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(ReleaseUrl));
        await new ReleaseCommand(runner.Context, processes).RunAsync();

        var arguments = processes.Calls.Single().Arguments;
        CollectionAssert.AreEqual(new[] { "release", "create", "v1.2.0", "--title" }, arguments.Take(4).ToList());
        CollectionAssert.DoesNotContain(arguments.ToList(), "--verify-tag");
    }

    [TestMethod]
    public async Task Run_DoesNothingWithoutNewlyUploadedPackages()
    {
        using var runner = new TestRunner();
        runner.State.NewlyUploaded = [];

        var processes = new FakeProcessRunner();
        await new ReleaseCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual(0, processes.Calls.Count);
        Assert.AreEqual("", runner.Output);
    }

    [TestMethod]
    public async Task Run_FailsWithGitHubError()
    {
        using var runner = new TestRunner();
        runner.State.NewlyUploaded = [runner.Entry("New", "1.0.0")];

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new ReleaseCommand(runner.Context, new FakeProcessRunner((_, _) => FakeProcessRunner.Failure("HTTP 422: tag exists"))).RunAsync());

        Assert.AreEqual("GitHub release creation failed: HTTP 422: tag exists", exception.Message);
    }

    [TestMethod]
    public void Title_SeparatesPrefixFromVersion()
    {
        Assert.AreEqual("Release 1.0.0", ReleaseCommand.Title("v1.0.0", "Release"));
        Assert.AreEqual("1.0.0", ReleaseCommand.Title("v1.0.0", ""));
        Assert.AreEqual("1.0.0", ReleaseCommand.Title("1.0.0", "  "));
    }
}
