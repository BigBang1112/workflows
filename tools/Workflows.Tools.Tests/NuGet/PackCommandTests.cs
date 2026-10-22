using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;
using Workflows.Tools.Processes;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.NuGet;

[TestClass]
public sealed class PackCommandTests
{
    private static ProcessResult Properties(string id, string version, string notes = "")
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Properties = new { PackageId = id, Version = version, PackageReleaseNotes = notes },
        });
        return FakeProcessRunner.Success(json);
    }

    [TestMethod]
    public async Task Prepare_MatchesPackedIdentityToProjectReleaseNotes()
    {
        using var runner = new TestRunner();
        runner.WriteFile("Src/PackageX/PackageX.csproj");
        runner.WriteFile("Templates/Templates.csproj");
        runner.WritePackage("Package.X", "1.0.0");

        var processes = new FakeProcessRunner((_, arguments) => arguments[1].Contains("PackageX")
            ? Properties("Package.X", "1.0.0", "\n      Fixed parsing.\n        - Indented detail\n    ")
            : Properties("Templates", "0.1.0"));

        await new PackCommand(runner.Context, processes).PrepareAsync([]);

        var entry = runner.State.Manifest.Single();
        Assert.AreEqual(Path.Combine("Src", "PackageX", "PackageX.csproj"), entry.Project);
        Assert.AreEqual("Fixed parsing.\n  - Indented detail", entry.Notes);
    }

    [TestMethod]
    public async Task Prepare_MatchesVersionsTheWayNuGetNormalizesThem()
    {
        using var runner = new TestRunner();
        runner.WriteFile("Lib/Lib.csproj");
        runner.WritePackage("Lib", "1.2.0");

        await new PackCommand(runner.Context, new FakeProcessRunner((_, _) => Properties("Lib", "1.2"))).PrepareAsync([]);

        Assert.AreEqual("1.2.0", runner.State.Manifest.Single().Version);
    }

    [TestMethod]
    public async Task Prepare_SkipsPackagesWithoutPrefix()
    {
        using var runner = new TestRunner();
        runner.Environment["PACKAGE_PREFIX"] = "company.";
        runner.WriteFile("A/A.csproj");
        runner.WriteFile("B/B.csproj");
        runner.WritePackage("Company.A", "1.0.0");
        runner.WritePackage("Other.B", "1.0.0");

        var processes = new FakeProcessRunner((_, arguments) => arguments[1].StartsWith('A')
            ? Properties("Company.A", "1.0.0")
            : Properties("Other.B", "1.0.0"));

        await new PackCommand(runner.Context, processes).PrepareAsync([]);

        Assert.AreEqual("Company.A", runner.State.Manifest.Single().Id);
    }

    [TestMethod]
    public async Task Prepare_FailsWhenNoPackageMatchesPrefix()
    {
        using var runner = new TestRunner();
        runner.Environment["PACKAGE_PREFIX"] = "Company.";
        runner.WriteFile("B/B.csproj");
        runner.WritePackage("Other.B", "1.0.0");

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new PackCommand(runner.Context, new FakeProcessRunner((_, _) => Properties("Other.B", "1.0.0"))).PrepareAsync([]));

        Assert.AreEqual("No NuGet packages were packed matching package-prefix 'Company.'", exception.Message);
    }

    [TestMethod]
    public async Task Prepare_FailsWhenNoProjectProducesPackage()
    {
        using var runner = new TestRunner();
        runner.WriteFile("A/A.csproj");
        runner.WritePackage("A", "2.0.0");

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new PackCommand(runner.Context, new FakeProcessRunner((_, _) => Properties("A", "1.0.0"))).PrepareAsync([]));

        Assert.AreEqual("No project matches packed package A 2.0.0", exception.Message);
    }

    [TestMethod]
    public async Task Prepare_FailsWhenSeveralProjectsProducePackage()
    {
        using var runner = new TestRunner();
        runner.WriteFile("A/A.csproj");
        runner.WriteFile("Copy/A.csproj");
        runner.WritePackage("A", "1.0.0");

        var exception = await Assert.ThrowsAsync<WorkflowException>(() =>
            new PackCommand(runner.Context, new FakeProcessRunner((_, _) => Properties("A", "1.0.0"))).PrepareAsync([]));

        Assert.AreEqual("Multiple projects produce A 1.0.0", exception.Message);
    }

    [TestMethod]
    public async Task Prepare_IgnoresProjectsInBuildOutput()
    {
        using var runner = new TestRunner();
        runner.WriteFile("A/A.csproj");
        runner.WriteFile("A/obj/Generated.csproj");
        runner.WritePackage("A", "1.0.0");

        var processes = new FakeProcessRunner((_, _) => Properties("A", "1.0.0"));
        await new PackCommand(runner.Context, processes).PrepareAsync([]);

        Assert.AreEqual(1, processes.Calls.Count);
    }

    [TestMethod]
    public async Task Run_PacksPackPathInsteadOfProjectPath()
    {
        using var runner = new TestRunner();
        runner.Environment["PROJECT_PATH"] = "src/*/*.csproj";
        runner.Environment["PACK_PATH"] = "src/Lib";
        runner.WriteFile("src/Lib/Lib.csproj");
        runner.WriteFile("src/App/App.csproj");

        var processes = new FakeProcessRunner((_, arguments) =>
        {
            if (arguments[0] == "pack")
            {
                runner.WritePackage("Lib", "1.0.0");
                return FakeProcessRunner.Success();
            }

            return Properties("Lib", "1.0.0");
        });

        await new PackCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                $"dotnet pack {Path.Combine("src", "Lib")} -c Release -p:ContinuousIntegrationBuild=true --output {runner.State.PackagesDirectory}",
                $"dotnet msbuild {Path.Combine("src", "Lib", "Lib.csproj")} -nologo -property:Configuration=Release -property:ContinuousIntegrationBuild=true -getProperty:PackageId,Version,PackageReleaseNotes",
            },
            processes.CommandLines.ToList());
    }
}
