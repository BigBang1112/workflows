using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.NuGet;

[TestClass]
public sealed class BuildAndTestCommandTests
{
    [TestMethod]
    public async Task Build_WithoutPathBuildsRepositoryRoot()
    {
        using var runner = new TestRunner();
        var processes = new FakeProcessRunner();

        await new BuildCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual("dotnet build -c Release -p:ContinuousIntegrationBuild=true", processes.CommandLines.Single());
    }

    [TestMethod]
    public async Task Build_BuildsEveryMatchOfEveryPattern()
    {
        using var runner = new TestRunner();
        runner.Environment["PROJECT_PATH"] = "src/*/*.csproj\n\nlib";
        runner.WriteFile("src/A/A.csproj");
        runner.WriteFile("src/B/B.csproj");
        runner.WriteFile("lib/Lib.csproj");
        var processes = new FakeProcessRunner();

        await new BuildCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                $"dotnet build {Path.Combine("src", "A", "A.csproj")} -c Release -p:ContinuousIntegrationBuild=true",
                $"dotnet build {Path.Combine("src", "B", "B.csproj")} -c Release -p:ContinuousIntegrationBuild=true",
                "dotnet build lib -c Release -p:ContinuousIntegrationBuild=true",
            },
            processes.CommandLines.ToList());
    }

    [TestMethod]
    public async Task Build_FailsWhenPatternMatchesNothing()
    {
        using var runner = new TestRunner();
        runner.Environment["PROJECT_PATH"] = "src/*.csproj";
        var processes = new FakeProcessRunner();

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new BuildCommand(runner.Context, processes).RunAsync());

        Assert.AreEqual("No projects match: src/*.csproj", exception.Message);
        Assert.AreEqual(0, processes.Calls.Count);
    }

    [TestMethod]
    public async Task Build_StopsAtFirstFailure()
    {
        using var runner = new TestRunner();
        runner.Environment["PROJECT_PATH"] = "*/*.csproj";
        runner.WriteFile("A/A.csproj");
        runner.WriteFile("B/B.csproj");
        var processes = new FakeProcessRunner((_, _) => FakeProcessRunner.Failure());

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new BuildCommand(runner.Context, processes).RunAsync());

        Assert.AreEqual($"dotnet build failed for {Path.Combine("A", "A.csproj")}", exception.Message);
        Assert.AreEqual(1, processes.Calls.Count);
    }

    [TestMethod]
    public async Task Test_CollectsCoverageWithVSTest()
    {
        using var runner = new TestRunner();
        runner.Environment["ENABLE_COVERAGE"] = "true";
        var processes = new FakeProcessRunner();

        await new TestCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(
            new[] { "test", "-c", "Release", "--collect", "Code Coverage;Format=cobertura;IncludeTestAssembly=false", "--results-directory", Path.Combine(runner.Temp, "coverage") },
            processes.Calls.Single().Arguments.ToList());
    }

    [TestMethod]
    public async Task Test_CollectsCoverageWithMicrosoftTestingPlatform()
    {
        using var runner = new TestRunner();
        runner.Environment["ENABLE_COVERAGE"] = "true";
        runner.Environment["TEST_PATH"] = "tests/*/*.csproj";
        runner.WriteFile("tests/A.Tests/A.Tests.csproj");
        runner.WriteFile("global.json", """
            {
              // Comments are allowed in global.json.
              "test": { "runner": "Microsoft.Testing.Platform" },
            }
            """);
        var processes = new FakeProcessRunner();

        await new TestCommand(runner.Context, processes).RunAsync();

        CollectionAssert.AreEqual(
            new[] { "test", Path.Combine("tests", "A.Tests", "A.Tests.csproj"), "-c", "Release", "--coverage", "--coverage-output-format", "cobertura", "--results-directory", Path.Combine(runner.Temp, "coverage") },
            processes.Calls.Single().Arguments.ToList());
    }

    [TestMethod]
    public async Task Test_WithoutCoverageOnlyPassesConfiguration()
    {
        using var runner = new TestRunner();
        runner.Environment["ENABLE_COVERAGE"] = "false";
        var processes = new FakeProcessRunner();

        await new TestCommand(runner.Context, processes).RunAsync();

        Assert.AreEqual("dotnet test -c Release", processes.CommandLines.Single());
    }
}
