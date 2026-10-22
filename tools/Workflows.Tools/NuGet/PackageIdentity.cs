using System.IO.Compression;
using System.Xml.Linq;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.NuGet;

internal sealed record PackageIdentity(string Id, string Version)
{
    /// <summary>Reads the package ID and version from the .nuspec inside a .nupkg file.</summary>
    public static PackageIdentity Read(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspecs = archive.Entries.Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToList();

        if (nuspecs.Count != 1)
        {
            throw new WorkflowException($"Expected one .nuspec in {packagePath}");
        }

        using var stream = nuspecs[0].Open();
        var metadata = XDocument.Load(stream).Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "metadata")
            ?? throw new WorkflowException($"Missing metadata in {packagePath}");

        var id = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "id")?.Value;
        var version = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "version")?.Value;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
        {
            throw new WorkflowException($"Missing package ID or version in {packagePath}");
        }

        return new PackageIdentity(id, version);
    }
}
