using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.Apps;

/// <summary>A file to package, with its path inside the zip and where it came from.</summary>
internal sealed record AssetFile(string Path, string Source, UnixFileMode Mode, string Origin);

/// <summary>The published files of one runner, like linux-x64.</summary>
internal sealed record Build(string Runner, string Directory, IReadOnlyList<AssetFile> Files);

/// <summary>
/// Packs the published files of each runner into zip assets for the release. The files of the package
/// directory are added to every zip, next to the published files.
/// </summary>
/// <remarks>
/// Artifacts don't keep file permissions, so each build arrives as a tar file. The zip entries get the modes
/// from the tar, so executables built on Linux and macOS stay executable after extraction.
/// </remarks>
internal sealed class PackageAppCommand(ActionsContext context)
{
    // S_IFREG, which zip tools expect next to the permissions of a regular file.
    private const int RegularFile = 0x8000;

    private const UnixFileMode DefaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private readonly AppPaths paths = new(context);

    public async Task RunAsync()
    {
        var name = context.Require("ASSET_NAME");
        var builds = Select(ReadBuilds(), context.GetLines("FILES"));
        var packageFiles = ReadPackageDirectory(context.Get("PACKAGE_DIRECTORY").Trim());

        List<(string Name, List<AssetFile> Files)> assets = context.GetBool("MERGE_RUNNERS", false)
            ? [($"{name}.zip", [.. packageFiles, .. builds.SelectMany(build => build.Files)])]
            : builds.Select(build => ($"{name}-{build.Runner}.zip", (List<AssetFile>)[.. packageFiles, .. build.Files])).ToList();

        Directory.CreateDirectory(paths.Assets);

        var summary = new StringBuilder();
        summary.Append("## Release assets\n\n");
        summary.Append("| Asset | Files | SHA-256 |\n");
        summary.Append("| --- | --- | --- |\n");

        foreach (var (assetName, files) in assets)
        {
            var path = Path.Combine(paths.Assets, assetName);
            WriteZip(path, assetName, files);

            await using var stream = File.OpenRead(path);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
            context.Log.WriteLine($"Packaged {assetName} with {files.Count} files, SHA-256 {hash}");
            summary.Append($"| {assetName} | {files.Count} | `{hash}` |\n");
        }

        context.AppendSummary(summary.ToString());
    }

    /// <summary>Extracts every tar file in the builds directory, sorted by runner.</summary>
    private List<Build> ReadBuilds()
    {
        var tars = Directory.Exists(paths.Builds)
            ? Directory.GetFiles(paths.Builds, "*.tar").Order(StringComparer.Ordinal).ToList()
            : [];

        if (tars.Count == 0)
        {
            throw new WorkflowException($"No builds found in {paths.Builds}");
        }

        return tars.Select(ReadBuild).ToList();
    }

    private Build ReadBuild(string tar)
    {
        var runner = Path.GetFileNameWithoutExtension(tar).ToLowerInvariant();
        var directory = Path.Combine(paths.Files, runner);
        var files = new List<AssetFile>();

        using var reader = new TarReader(File.OpenRead(tar));

        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType == TarEntryType.Directory)
            {
                continue;
            }

            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile))
            {
                throw new WorkflowException($"{entry.Name} in the {runner} build is a {entry.EntryType} entry. Only regular files can be packaged.");
            }

            var path = EntryPath(entry.Name);
            var source = Path.GetFullPath(Path.Combine(directory, path));

            if (!source.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new WorkflowException($"{entry.Name} in the {runner} build is outside the published output");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            entry.ExtractToFile(source, overwrite: true);
            files.Add(new AssetFile(path, source, Normalize(entry.Mode), $"the {runner} build"));
        }

        return new Build(runner, directory, files);
    }

    /// <summary>
    /// Keeps the files that match any of the patterns. Every pattern must match a file in some build,
    /// and every build must keep at least one file. Without patterns, every file is kept.
    /// </summary>
    internal static List<Build> Select(IReadOnlyList<Build> builds, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return builds.ToList();
        }

        var selected = builds.ToDictionary(build => build, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        foreach (var pattern in patterns)
        {
            var matcher = new Matcher();
            matcher.AddInclude(pattern);
            var matched = false;

            foreach (var build in builds)
            {
                var result = matcher.Match(build.Directory, build.Files.Select(file => file.Source));
                selected[build].UnionWith(result.Files.Select(file => file.Path));
                matched |= result.HasMatches;
            }

            if (!matched)
            {
                throw new WorkflowException($"No published files match: {pattern}");
            }
        }

        return builds.Select(build =>
        {
            var files = build.Files.Where(file => selected[build].Contains(file.Path)).ToList();

            return files.Count > 0
                ? build with { Files = files }
                : throw new WorkflowException($"No published files of the {build.Runner} build match the files input");
        }).ToList();
    }

    private List<AssetFile> ReadPackageDirectory(string directory)
    {
        if (directory.Length == 0)
        {
            return [];
        }

        var root = context.ResolvePath(directory);

        if (!Directory.Exists(root))
        {
            throw new WorkflowException($"Package directory not found: {directory}");
        }

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(source => new AssetFile(
                Path.GetRelativePath(root, source).Replace('\\', '/'),
                source,
                OperatingSystem.IsWindows() ? DefaultMode : Normalize(File.GetUnixFileMode(source)),
                "the package directory"))
            .ToList();
    }

    /// <summary>Writes the zip sorted by path. Two files with the same path, ignoring case, fail the step.</summary>
    private static void WriteZip(string path, string assetName, IReadOnlyList<AssetFile> files)
    {
        var duplicate = files.GroupBy(file => file.Path, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            var origins = duplicate.Select(file => file.Origin).ToList();
            throw new WorkflowException($"{assetName} would contain {duplicate.Key} from both {origins[0]} and {origins[1]}");
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var file in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            var entry = archive.CreateEntryFromFile(file.Source, file.Path, CompressionLevel.SmallestSize);
            entry.ExternalAttributes = (RegularFile | (int)file.Mode) << 16;
        }
    }

    /// <summary>
    /// Keeps only whether a file is executable, like Git does. Tar on Windows records modes such as 0600,
    /// which would make the files unreadable for other users after extraction on Linux.
    /// </summary>
    internal static UnixFileMode Normalize(UnixFileMode mode) => (mode & AnyExecute) != 0 ? DefaultMode | AnyExecute : DefaultMode;

    /// <summary>Tar entries from "tar -cf - ." start with "./".</summary>
    private static string EntryPath(string name)
    {
        var path = name.Replace('\\', '/');

        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        return path.TrimStart('/');
    }
}
