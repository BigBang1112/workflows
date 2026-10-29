using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.Apps;

/// <summary>
/// Creates the GitHub release with the packaged zip assets. The tag is the tag input, otherwise the pushed tag.
/// </summary>
/// <remarks>
/// The notes are the description, then the notes GitHub generates when asked to, then a link to the workflow run.
/// The release is created complete in a single call, which works with immutable releases. Outputs <c>url</c>.
/// </remarks>
internal sealed class ReleaseAppCommand(ActionsContext context, IProcessRunner processes, GitHubApi github)
{
    private readonly AppPaths paths = new(context);
    private readonly GitHubRelease release = new(context, processes);

    public async Task RunAsync()
    {
        var pushedTag = context.Get("GITHUB_REF_TYPE") == "tag" ? context.Get("GITHUB_REF_NAME") : "";
        var tag = context.Get("TAG").Trim() is { Length: > 0 } input ? input : pushedTag;

        if (tag.Length == 0)
        {
            throw new WorkflowException("Set the tag input, or run the workflow for a pushed tag");
        }

        var assets = Directory.Exists(paths.Assets)
            ? Directory.GetFiles(paths.Assets, "*.zip").Order(StringComparer.Ordinal).ToList()
            : [];

        if (assets.Count == 0)
        {
            throw new WorkflowException($"No assets found in {paths.Assets}");
        }

        List<string> sections = [];

        if (context.Get("RELEASE_DESCRIPTION").Trim() is { Length: > 0 } description)
        {
            sections.Add(description);
        }

        if (context.GetBool("GENERATE_RELEASE_NOTES", false))
        {
            sections.Add(await GenerateNotesAsync(tag));
        }

        sections.Add(GitHubRelease.GeneratedBy(release.RunUrl));
        await File.WriteAllTextAsync(paths.ReleaseNotes, string.Join("\n\n", sections) + "\n");

        var title = GitHubRelease.Title(tag, context.Get("RELEASE_TITLE_PREFIX"));
        var url = await release.CreateAsync(tag, assets, title, paths.ReleaseNotes, verifyTag: tag == pushedTag);

        context.Log.WriteLine($"Created {url} with {assets.Count} assets");
        context.SetOutput("url", url);
    }

    /// <summary>Asks GitHub for the notes it would generate, so they can come before the link to the run.</summary>
    private async Task<string> GenerateNotesAsync(string tag)
    {
        var notes = await github.PostAsync($"repos/{context.Require("GITHUB_REPOSITORY")}/releases/generate-notes", new
        {
            tag_name = tag,
            target_commitish = context.Require("GITHUB_SHA"),
        });

        return (notes.GetProperty("body").GetString() ?? "").Trim();
    }
}
