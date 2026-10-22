namespace Workflows.Tools.NuGet;

/// <summary>
/// Composes the GitHub release notes and the Discord message for newly uploaded packages.
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
    /// With a main project, its notes come first without a heading. Every other package gets a
    /// "### ID Version" heading, sorted by ID. A link to the workflow run comes last.
    /// </summary>
    public static string ReleaseNotes(IReadOnlyList<PackageEntry> packages, string mainProject, string runUrl)
    {
        var main = mainProject.Trim().Length > 0 ? SelectMainPackage(packages, mainProject) : null;
        var sections = new List<string>();

        if (main is not null)
        {
            sections.Add(NotesOf(main));
        }

        sections.AddRange(packages
            .Where(package => package != main)
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Section));

        sections.Add($"Assets were automatically generated using the [publish workflow](<{runUrl}>).");
        return string.Join("\n\n", sections) + "\n";
    }

    /// <summary>
    /// The main package gets a "## ID Version" heading followed by the top lines and its notes.
    /// Other packages follow in their original order, then the links and the bottom lines.
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

        mainSection.Add(NotesOf(main));

        List<string> sections = [string.Join("\n\n", mainSection), .. packages.Where(package => package != main).Select(Section)];
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
