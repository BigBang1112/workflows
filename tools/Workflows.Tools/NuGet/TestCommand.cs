using System.Text.Json;
using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Tests the projects from TEST_PATH, or the repository root when it is empty.
/// With ENABLE_COVERAGE, Cobertura coverage goes to $RUNNER_TEMP/coverage, the same as in dotnet-ci.yml.
/// </summary>
internal sealed class TestCommand(ActionsContext context, IProcessRunner processes)
{
    public Task RunAsync()
    {
        List<string> options = ["-c", "Release"];

        if (context.GetBool("ENABLE_COVERAGE", false))
        {
            options.AddRange(UsesMicrosoftTestingPlatform(context.WorkingDirectory)
                ? ["--coverage", "--coverage-output-format", "cobertura"]
                : ["--collect", "Code Coverage;Format=cobertura;IncludeTestAssembly=false"]);
            options.AddRange(["--results-directory", context.TempPath("coverage")]);
        }

        return DotnetTargets.RunAsync(context, processes, "test", context.GetLines("TEST_PATH"), "test projects", options);
    }

    /// <summary>
    /// dotnet test uses Microsoft Testing Platform when global.json selects it as the test runner.
    /// </summary>
    public static bool UsesMicrosoftTestingPlatform(string directory)
    {
        var path = Path.Combine(directory, "global.json");

        if (!File.Exists(path))
        {
            return false;
        }

        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        return json.RootElement.TryGetProperty("test", out var test)
            && test.TryGetProperty("runner", out var runner)
            && string.Equals(runner.GetString(), "Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase);
    }
}
