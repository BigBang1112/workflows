using Workflows.Tools.GitHub;

namespace Workflows.Tools.Apps;

/// <summary>
/// Paths in the runner's temp directory that pass files from one app publishing step to the next.
/// The workflow downloads the builds to <see cref="Builds"/> and uploads <see cref="Assets"/>.
/// </summary>
internal sealed class AppPaths(ActionsContext context)
{
    /// <summary>One tar file of published output per runner, named after its OS and architecture.</summary>
    public string Builds => context.TempPath("app-builds");

    /// <summary>The files extracted from the builds, in a directory per runner.</summary>
    public string Files => context.TempPath("app-files");

    public string Assets => context.TempPath("app-assets");

    public string ReleaseNotes => context.TempPath("app-release-notes.md");
}
