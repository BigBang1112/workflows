using Workflows.Tools.GitHub;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Composes the GitHub release notes and the Discord message for the selected packages.
/// </summary>
internal static class Announcement
{
    private const string NoNotes = "*No release notes provided.*";

    /// <summary>
    /// Picks the package that names the release: the one whose ID or project directory matches
    /// <paramref name="mainProject"/>, otherwise the one with the highest version.
    /// </summary>
    public static PackageEntry SelectMainPackage(IReadOnlyList<PackageEntry> packages, string mainProject)
    {
        mainProject = mainProject.Trim();

        var match = mainProject.Length == 0 ? null : packages.FirstOrDefault(package =>
            package.Id.Equals(mainProject, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(Path.GetDirectoryName(package.Project) ?? "").Equals(mainProject, StringComparison.OrdinalIgnoreCase));

        return match ?? packages.Aggregate((highest, package) => PackageVersion.Compare(package.Version, highest.Version) > 0 ? package : highest);
    }

    /// <summary>
    /// The description comes first. With a main project, its notes follow without a heading. Every other
    /// package gets a "### ID Version" heading, sorted by ID. A link to the workflow run comes last.
    /// </summary>
    public static string ReleaseNotes(IReadOnlyList<PackageEntry> packages, string mainProject, string runUrl, string description = "")
    {
        var main = mainProject.Trim().Length > 0 ? SelectMainPackage(packages, mainProject) : null;
        var sections = new List<string>();

        if (description.Trim() is { Length: > 0 } trimmedDescription)
        {
            sections.Add(trimmedDescription);
        }

        if (main is not null)
        {
            sections.Add(NotesOf(main));
        }

        sections.AddRange(packages
            .Where(package => package != main)
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Section));

        sections.Add(GitHubRelease.GeneratedBy(runUrl));
        return string.Join("\n\n", sections) + "\n";
    }

    /// <summary>
    /// The main package gets a "## ID Version" heading followed by the top lines and its notes.
    /// Other packages with notes follow in their original order, then the links and the bottom lines.
    /// </summary>
    public static string DiscordMessage(
        IReadOnlyList<PackageEntry> packages,
        PackageEntry main,
        string releaseUrl,
        bool nugetAvailable,
        string topLines,
        string bottomLines)
    {
        List<string> mainSection = [$"## {main.Id} {main.Version}"];

        if (topLines.Trim() is { Length: > 0 } top)
        {
            mainSection.Add(top);
        }

        if (main.Notes.Length > 0)
        {
            mainSection.Add(main.Notes);
        }

        // Discord already spaces headings, so the notes follow the heading directly.
        List<string> sections =
        [
            string.Join("\n\n", mainSection),
            .. packages
                .Where(package => package != main && package.Notes.Length > 0)
                .Select(package => $"### {package.Id} {package.Version}\n{package.Notes}"),
        ];
        List<string> links = [$"GitHub: <{releaseUrl}>"];

        if (nugetAvailable)
        {
            links.Add($"NuGet: <https://www.nuget.org/packages/{main.Id}/{main.Version}>");
        }

        sections.Add(string.Join("\n", links));

        if (bottomLines.Trim() is { Length: > 0 } bottom)
        {
            sections.Add(bottom);
        }

        return string.Join("\n\n", sections) + "\n";
    }

    private static string Section(PackageEntry package) => $"### {package.Id} {package.Version}\n\n{NotesOf(package)}";

    private static string NotesOf(PackageEntry package) => package.Notes.Length > 0 ? package.Notes : NoNotes;
}
