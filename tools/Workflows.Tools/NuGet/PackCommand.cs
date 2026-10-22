using System.Text.Json;
using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Packs the projects from PACK_PATH (or PROJECT_PATH, or the repository root) into the packages directory,
/// then matches every package with PACKAGE_PREFIX to the project that produced it.
/// The package's actual ID and version decide the match, and the project's PackageReleaseNotes become its notes.
/// </summary>
internal sealed class PackCommand(ActionsContext context, IProcessRunner processes)
{
    private readonly PublishState state = new(context);

    public async Task RunAsync()
    {
        var patterns = context.GetLines("PACK_PATH") is { Count: > 0 } packPatterns ? packPatterns : context.GetLines("PROJECT_PATH");

        await DotnetTargets.RunAsync(
            context,
            processes,
            "pack",
            patterns,
            "projects to pack",
            ["-c", "Release", "-p:ContinuousIntegrationBuild=true", "--output", state.PackagesDirectory]);

        await PrepareAsync(patterns);
    }

    internal async Task PrepareAsync(IReadOnlyList<string> patterns)
    {
        var prefix = context.Get("PACKAGE_PREFIX");
        var projectsByIdentity = new Dictionary<(string Id, string Version), List<(string Project, string Notes)>>();

        foreach (var project in ProjectPatterns.ResolveProjectFiles(context.WorkingDirectory, patterns))
        {
            var properties = await ReadPropertiesAsync(project);
            var id = properties.GetValueOrDefault("PackageId") is { Length: > 0 } packageId ? packageId : Path.GetFileNameWithoutExtension(project);
            var key = (id.ToLowerInvariant(), PackageVersion.Normalize(properties.GetValueOrDefault("PackageVersion", "")));

            projectsByIdentity.TryAdd(key, []);
            projectsByIdentity[key].Add((project, properties.GetValueOrDefault("PackageReleaseNotes", "")));
        }

        var packages = Directory.Exists(state.PackagesDirectory)
            ? Directory.EnumerateFiles(state.PackagesDirectory, "*.nupkg")
                .Order(StringComparer.Ordinal)
                .Select(path => (Path: path, Identity: PackageIdentity.Read(path)))
                .Where(package => package.Identity.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];

        if (packages.Count == 0)
        {
            throw new WorkflowException(prefix.Length > 0
                ? $"No NuGet packages were packed matching package-prefix '{prefix}'"
                : "No NuGet packages were packed");
        }

        var manifest = new List<PackageEntry>();

        foreach (var (path, (id, version)) in packages)
        {
            var matches = projectsByIdentity.GetValueOrDefault((id.ToLowerInvariant(), PackageVersion.Normalize(version)), []);

            if (matches.Count == 0)
            {
                throw new WorkflowException($"No project matches packed package {id} {version}");
            }

            if (matches.Count > 1)
            {
                throw new WorkflowException($"Multiple projects produce {id} {version}");
            }

            var (project, notes) = matches[0];
            manifest.Add(new PackageEntry(path, id, version, project, Text.Dedent(notes).Trim()));
        }

        state.Manifest = manifest;
        context.Log.WriteLine($"Prepared metadata for {manifest.Count} packages");
    }

    private async Task<Dictionary<string, string>> ReadPropertiesAsync(string project)
    {
        var result = await processes.RunAsync("dotnet",
        [
            "msbuild", project, "-nologo",
            "-property:Configuration=Release", "-property:ContinuousIntegrationBuild=true",
            // PackageVersion is the version dotnet pack writes. It defaults to Version, which is
            // built from VersionPrefix and VersionSuffix.
            "-getProperty:PackageId,PackageVersion,PackageReleaseNotes",
        ], echo: false);

        if (result.ExitCode != 0)
        {
            throw new WorkflowException($"Could not read package properties from {project}: {result.Output.Trim()}");
        }

        using var json = JsonDocument.Parse(result.StandardOutput);
        return json.RootElement.GetProperty("Properties")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? "");
    }
}
