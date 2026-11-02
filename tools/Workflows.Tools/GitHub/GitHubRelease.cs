using Workflows.Tools.Processes;

namespace Workflows.Tools.GitHub;

/// <summary>
/// Creates a GitHub release complete in a single call, with its notes and assets, which works with immutable releases.
/// </summary>
internal sealed class GitHubRelease(ActionsContext context, IProcessRunner processes)
{
    public string RunUrl => $"{ServerUrl()}/{context.Require("GITHUB_REPOSITORY")}/actions/runs/{context.Require("GITHUB_RUN_ID")}";

    /// <summary>The last line of the release notes, linking the run that generated the assets.</summary>
    public static string GeneratedBy(string runUrl) => $"Assets were automatically generated using the [publish workflow](<{runUrl}>).";

    public static string Title(string tag, string prefix)
    {
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        return prefix.Trim() is { Length: > 0 } trimmed ? $"{trimmed} {version}" : version;
    }

    /// <summary>
    /// Creates the release for <paramref name="tag"/> at the current commit and returns its URL.
    /// With <paramref name="verifyTag"/>, the tag must already exist. Otherwise, GitHub creates it.
    /// </summary>
    public async Task<string> CreateAsync(string tag, IEnumerable<string> assets, string title, string notesPath, bool verifyTag)
    {
        List<string> arguments =
        [
            "release", "create", tag,
            .. assets,
            "--title", title,
            "--notes-file", notesPath,
            "--target", context.Require("GITHUB_SHA"),
            "--repo", context.Require("GITHUB_REPOSITORY"),
        ];

        if (verifyTag)
        {
            arguments.Add("--verify-tag");
        }

        var result = await processes.RunAsync("gh", arguments, echo: false);

        if (result.ExitCode != 0)
        {
            throw new WorkflowException($"GitHub release creation failed: {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Trim();
    }

    private string ServerUrl() => context.Get("GITHUB_SERVER_URL") is { Length: > 0 } url ? url : "https://github.com";
}
