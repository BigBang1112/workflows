using System.Text.Encodings.Web;
using System.Text.Json;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.NuGet;

/// <summary>A packed package matched to the project that produced it.</summary>
internal sealed record PackageEntry(string Path, string Id, string Version, string Project, string Notes);

/// <summary>The push status of one package for each feed.</summary>
internal sealed record PublishResult(string Id, string Version, Dictionary<string, PushStatus> Feeds);

/// <summary>
/// Files that pass results from one publish step to the next. They live in the runner's temp directory,
/// next to the packed packages.
/// </summary>
internal sealed class PublishState(ActionsContext context)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string PackagesDirectory => context.TempPath("nuget-packages");

    public string ReleaseNotesPath => context.TempPath("release-notes.md");

    public IReadOnlyList<PackageEntry> Manifest
    {
        get => Read<List<PackageEntry>>("manifest.json");
        set => Write("manifest.json", value);
    }

    public IReadOnlyList<PackageEntry> NewlyUploaded
    {
        get => Read<List<PackageEntry>>("newly-uploaded.json");
        set => Write("newly-uploaded.json", value);
    }

    public IReadOnlyList<PublishResult> Results
    {
        get => Read<List<PublishResult>>("publish-results.json");
        set => Write("publish-results.json", value);
    }

    private T Read<T>(string name)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(PackagesDirectory, name)), JsonOptions)
            ?? throw new WorkflowException($"{name} is empty");
    }

    private void Write<T>(string name, T value)
    {
        Directory.CreateDirectory(PackagesDirectory);
        File.WriteAllText(Path.Combine(PackagesDirectory, name), JsonSerializer.Serialize(value, JsonOptions));
    }
}
