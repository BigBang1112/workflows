using Microsoft.Extensions.FileSystemGlobbing;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Resolves the newline-separated project paths and globs from workflow inputs.
/// </summary>
internal static class ProjectPatterns
{
    /// <summary>
    /// Resolves each pattern to matching paths relative to <paramref name="root"/>.
    /// A pattern without wildcards that names an existing directory resolves to that directory,
    /// which dotnet commands accept like a project or solution.
    /// </summary>
    public static IReadOnlyList<string> Resolve(string root, IEnumerable<string> patterns, string kind)
    {
        var results = new List<string>();

        foreach (var pattern in patterns)
        {
            var matches = Match(root, pattern);

            if (matches.Count == 0)
            {
                throw new WorkflowException($"No {kind} match: {pattern}");
            }

            results.AddRange(matches);
        }

        return results.Distinct().ToList();
    }

    /// <summary>
    /// Resolves patterns to project files. Directories resolve to the project files directly inside them.
    /// Without patterns, every *.csproj outside bin and obj directories is used.
    /// </summary>
    public static IReadOnlyList<string> ResolveProjectFiles(string root, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return Match(root, "**/*.csproj")
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                .ToList();
        }

        return Resolve(root, patterns, "projects")
            .SelectMany(path => Directory.Exists(Path.Combine(root, path))
                ? Directory.EnumerateFiles(Path.Combine(root, path), "*.*proj").Select(file => Path.GetRelativePath(root, file))
                : [path])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> Match(string root, string pattern)
    {
        if (!HasWildcards(pattern) && Directory.Exists(Path.Combine(root, pattern)))
        {
            return [Path.GetRelativePath(root, Path.Combine(root, pattern))];
        }

        var matcher = new Matcher();
        matcher.AddInclude(pattern);

        return matcher.GetResultsInFullPath(root)
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool HasWildcards(string pattern) => pattern.IndexOfAny(['*', '?', '[']) >= 0;
}
