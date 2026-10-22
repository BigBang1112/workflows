using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.NuGet;

internal static class DotnetTargets
{
    /// <summary>
    /// Runs a dotnet command once for each path matching the patterns,
    /// or once in the working directory when there are no patterns.
    /// </summary>
    public static async Task RunAsync(
        ActionsContext context,
        IProcessRunner processes,
        string command,
        IReadOnlyList<string> patterns,
        string kind,
        IReadOnlyList<string> options)
    {
        List<string?> targets = patterns.Count == 0
            ? [null]
            : [.. ProjectPatterns.Resolve(context.WorkingDirectory, patterns, kind)];

        foreach (var target in targets)
        {
            List<string> arguments = target is null ? [command, .. options] : [command, target, .. options];
            var result = await processes.RunAsync("dotnet", arguments);

            if (result.ExitCode != 0)
            {
                throw new WorkflowException(target is null ? $"dotnet {command} failed" : $"dotnet {command} failed for {target}");
            }
        }
    }
}
