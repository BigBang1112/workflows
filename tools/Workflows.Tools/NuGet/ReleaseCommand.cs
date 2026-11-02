using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Creates one GitHub release for the selected packages, with their packages as assets.
/// The tag is the pushed tag when the workflow runs for one, otherwise "v" and the main package's version.
/// </summary>
/// <remarks>
/// The release is created complete in a single call, which works with immutable releases.
/// Outputs <c>created</c> and <c>url</c>.
/// </remarks>
internal sealed class ReleaseCommand(ActionsContext context, IProcessRunner processes)
{
    private readonly PublishState state = new(context);
    private readonly GitHubRelease release = new(context, processes);

    public async Task RunAsync()
    {
        var packages = state.ReleasePackages;

        if (packages.Count == 0)
        {
            return;
        }

        var mainProject = context.Get("MAIN_PROJECT");
        var notes = Announcement.ReleaseNotes(packages, mainProject, release.RunUrl, context.Get("RELEASE_DESCRIPTION"));
        await File.WriteAllTextAsync(state.ReleaseNotesPath, notes);

        var isTag = context.Get("GITHUB_REF_TYPE") == "tag";
        var tag = isTag ? context.Require("GITHUB_REF_NAME") : $"v{Announcement.SelectMainPackage(packages, mainProject).Version}";

        List<string> assets = [];

        if (context.GetBool("UPLOAD_TO_RELEASE", true))
        {
            foreach (var package in packages)
            {
                assets.Add(package.Path);

                var symbolsPath = Path.ChangeExtension(package.Path, ".snupkg");

                if (File.Exists(symbolsPath))
                {
                    assets.Add(symbolsPath);
                }
            }
        }

        var title = GitHubRelease.Title(tag, context.Get("RELEASE_TITLE_PREFIX"));
        var url = await release.CreateAsync(tag, assets, title, state.ReleaseNotesPath, verifyTag: isTag);

        context.Log.WriteLine($"Created {url} with {packages.Count} packages");
        context.SetOutput("created", "true");
        context.SetOutput("url", url);
    }
}
