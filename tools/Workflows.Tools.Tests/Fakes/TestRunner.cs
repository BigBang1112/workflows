using System.IO.Compression;
using System.Text;
using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;

namespace Workflows.Tools.Tests.Fakes;

/// <summary>
/// A fake runner with its own workspace, temp directory, step output, and job summary.
/// </summary>
internal sealed class TestRunner : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("workflows-tools-").FullName;

    public TestRunner()
    {
        Directory.CreateDirectory(Workspace);
        Directory.CreateDirectory(Temp);

        Environment["RUNNER_TEMP"] = Temp;
        Environment["GITHUB_OUTPUT"] = Path.Combine(root, "output.txt");
        Environment["GITHUB_STEP_SUMMARY"] = Path.Combine(root, "summary.md");
        Environment["GITHUB_REPOSITORY"] = "owner/repo";
        Environment["GITHUB_REPOSITORY_OWNER"] = "owner";
        Environment["GITHUB_RUN_ID"] = "123";
        Environment["GITHUB_SHA"] = "abc123";
    }

    public string Workspace => Path.Combine(root, "workspace");

    public string Temp => Path.Combine(root, "temp");

    public Dictionary<string, string> Environment { get; } = [];

    public StringWriter Log { get; } = new();

    public ActionsContext Context => new(Environment, Workspace, Log);

    public PublishState State => new(Context);

    public string Output => ReadIfExists(Environment["GITHUB_OUTPUT"]);

    public string Summary => ReadIfExists(Environment["GITHUB_STEP_SUMMARY"]);

    public string WriteFile(string relativePath, string content = "")
    {
        var path = Path.Combine(Workspace, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Creates a minimal .nupkg in the packages directory.</summary>
    public string WritePackage(string id, string version, bool withSymbols = false)
    {
        Directory.CreateDirectory(State.PackagesDirectory);
        var path = Path.Combine(State.PackagesDirectory, $"{id}.{version}.nupkg");

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open(), Encoding.UTF8);
            writer.Write($"""
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata><id>{id}</id><version>{version}</version></metadata>
                </package>
                """);
        }

        if (withSymbols)
        {
            File.WriteAllText(Path.ChangeExtension(path, ".snupkg"), "");
        }

        return path;
    }

    public PackageEntry Entry(string id, string version, string notes = "", string project = "")
    {
        return new PackageEntry(Path.Combine(State.PackagesDirectory, $"{id}.{version}.nupkg"), id, version, project, notes);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private static string ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : "";
}
