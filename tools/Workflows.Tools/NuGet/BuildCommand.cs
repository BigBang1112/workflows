using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Builds the projects from PROJECT_PATH, or the repository root when it is empty.
/// </summary>
internal sealed class BuildCommand(ActionsContext context, IProcessRunner processes)
{
    public Task RunAsync()
    {
        return DotnetTargets.RunAsync(
            context,
            processes,
            "build",
            context.GetLines("PROJECT_PATH"),
            "projects",
            ["-c", "Release", "-p:ContinuousIntegrationBuild=true"]);
    }
}
