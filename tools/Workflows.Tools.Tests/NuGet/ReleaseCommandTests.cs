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
    public async Task Run_AllPackagesIncludesExistingAssetsAndNotes()
    {
        using var runner = new TestRunner();
        runner.Environment["RELEASE_ALL_PACKAGES"] = "true";
        runner.Environment["MAIN_PROJECT"] = "Existing";
        var newPackage = runner.WritePackage("New", "1.0.0");
        var existingPackage = runner.WritePackage("Existing", "2.0.0", withSymbols: true);
        runner.State.Manifest = [runner.Entry("New", "1.0.0", "New notes"), runner.Entry("Existing", "2.0.0", "Existing notes")];
        runner.State.NewlyUploaded = [runner.State.Manifest[0]];

        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(ReleaseUrl));
        await new ReleaseCommand(runner.Context, processes).RunAsync();

        var arguments = processes.Calls.Single().Arguments.ToList();
        CollectionAssert.AreEqual(
            new[] { "release", "create", "v2.0.0", newPackage, existingPackage, Path.ChangeExtension(existingPackage, ".snupkg") },
            arguments.Take(6).ToList());
        var notes = File.ReadAllText(runner.State.ReleaseNotesPath);
        StringAssert.StartsWith(notes, "Existing notes\n\n");
        StringAssert.Contains(notes, "### New 1.0.0\n\nNew notes");
        StringAssert.Contains(runner.Output, "created=true");
    }

    [TestMethod]
    public async Task Run_AllDuplicatePushesCanStillCreateReleaseWithAssets()
    {
        using var runner = new TestRunner();
        runner.Environment["RELEASE_ALL_PACKAGES"] = "true";
        runner.Environment["PUSH_TO_GITHUB"] = "false";
        runner.Environment["NUGET_API_KEY"] = "nuget-key";
        var package = runner.WritePackage("Existing", "1.0.0", withSymbols: true);
        runner.State.Manifest = [runner.Entry("Existing", "1.0.0")];

        var processes = new FakeProcessRunner((fileName, _) => FakeProcessRunner.Success(
            fileName == "gh" ? ReleaseUrl : "409 Conflict: package already exists"));
        await new PublishCommand(runner.Context, processes).RunAsync();
        await new ReleaseCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual(0, runner.State.NewlyUploaded.Count);
        StringAssert.Contains(runner.Output, "has-new=false");
        StringAssert.Contains(runner.Output, "has-failures=false");
        StringAssert.Contains(runner.Output, "created=true");
        var arguments = processes.Calls.Single(call => call.FileName == "gh").Arguments.ToList();
        CollectionAssert.Contains(arguments, package);
        CollectionAssert.Contains(arguments, Path.ChangeExtension(package, ".snupkg"));
    }

    [TestMethod]
    public async Task Run_AllPackagesStillHonorsDisabledAssets()
    {
        using var runner = new TestRunner();
        runner.Environment["RELEASE_ALL_PACKAGES"] = "true";
        runner.Environment["UPLOAD_TO_RELEASE"] = "false";
        var package = runner.WritePackage("Existing", "1.0.0", withSymbols: true);
        runner.State.Manifest = [runner.Entry("Existing", "1.0.0")];
        runner.State.NewlyUploaded = [];

        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Success(ReleaseUrl));
        await new ReleaseCommand(runner.Context, processes).RunAsync();

        var arguments = processes.Calls.Single().Arguments.ToList();
        CollectionAssert.DoesNotContain(arguments, package);
        CollectionAssert.DoesNotContain(arguments, Path.ChangeExtension(package, ".snupkg"));
        StringAssert.Contains(runner.Output, "created=true");
    }

    [TestMethod]
    public async Task Run_AllPackagesDoesNothingWithoutPackedPackages()
    {
        using var runner = new TestRunner();
        runner.Environment["RELEASE_ALL_PACKAGES"] = "true";
        runner.State.Manifest = [];

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
}
