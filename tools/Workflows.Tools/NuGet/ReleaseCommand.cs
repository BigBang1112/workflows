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

    public async Task RunAsync()
    {
        var packages = state.ReleasePackages;

        if (packages.Count == 0)
        {
            return;
        }

        var mainProject = context.Get("MAIN_PROJECT");
        var runUrl = $"{ServerUrl()}/{context.Require("GITHUB_REPOSITORY")}/actions/runs/{context.Require("GITHUB_RUN_ID")}";
        var notes = Announcement.ReleaseNotes(packages, mainProject, runUrl, context.Get("RELEASE_DESCRIPTION"));
        await File.WriteAllTextAsync(state.ReleaseNotesPath, notes);

        var isTag = context.Get("GITHUB_REF_TYPE") == "tag";
        var tag = isTag ? context.Require("GITHUB_REF_NAME") : $"v{Announcement.SelectMainPackage(packages, mainProject).Version}";

        List<string> arguments = ["release", "create", tag];

        if (context.GetBool("UPLOAD_TO_RELEASE", true))
        {
            foreach (var package in packages)
            {
                arguments.Add(package.Path);

                var symbolsPath = Path.ChangeExtension(package.Path, ".snupkg");

                if (File.Exists(symbolsPath))
                {
                    arguments.Add(symbolsPath);
                }
            }
        }

        arguments.AddRange([
            "--title", Title(tag, context.Get("RELEASE_TITLE_PREFIX")),
            "--notes-file", state.ReleaseNotesPath,
            "--target", context.Require("GITHUB_SHA"),
            "--repo", context.Require("GITHUB_REPOSITORY"),
        ]);

        if (isTag)
        {
            arguments.Add("--verify-tag");
        }

        var result = await processes.RunAsync("gh", arguments, echo: false);

        if (result.ExitCode != 0)
        {
            throw new WorkflowException($"GitHub release creation failed: {result.StandardError.Trim()}");
        }

        var url = result.StandardOutput.Trim();
        context.Log.WriteLine($"Created {url} with {packages.Count} packages");
        context.SetOutput("created", "true");
        context.SetOutput("url", url);
    }

    internal static string Title(string tag, string prefix)
    {
        var version = tag.StartsWith('v') ? tag[1..] : tag;
        return prefix.Trim() is { Length: > 0 } trimmed ? $"{trimmed} {version}" : version;
    }

    private string ServerUrl() => context.Get("GITHUB_SERVER_URL") is { Length: > 0 } url ? url : "https://github.com";
}
