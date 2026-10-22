using Workflows.Tools.NuGet;

namespace Workflows.Tools.Tests.NuGet;

[TestClass]
public sealed class AnnouncementTests
{
    private const string RunUrl = "https://github.com/owner/repo/actions/runs/123";
    private const string Footer = $"Assets were automatically generated using the [publish workflow](<{RunUrl}>).\n";

    private static PackageEntry Package(string id, string version, string notes = "", string project = "")
    {
        return new PackageEntry($"{id}.{version}.nupkg", id, version, project, notes);
    }

    [TestMethod]
    public void ReleaseNotes_MainProjectNotesComeFirstWithoutHeading()
    {
        var notes = Announcement.ReleaseNotes(
            [Package("Package.Y", "9.0.0", "Added a feature."), Package("Package.X", "1.0.0", "Fixed a bug.")],
            "Package.X",
            RunUrl);

        Assert.AreEqual("Fixed a bug.\n\n### Package.Y 9.0.0\n\nAdded a feature.\n\n" + Footer, notes);
    }

    [TestMethod]
    public void ReleaseNotes_WithoutMainProjectEveryPackageHasHeadingSortedById()
    {
        var notes = Announcement.ReleaseNotes(
            [Package("Package.Y", "9.0.0", "Added a feature."), Package("Package.X", "2.0.0", "Fixed a bug.")],
            "",
            RunUrl);

        Assert.AreEqual("### Package.X 2.0.0\n\nFixed a bug.\n\n### Package.Y 9.0.0\n\nAdded a feature.\n\n" + Footer, notes);
    }

    [TestMethod]
    public void ReleaseNotes_DescriptionComesFirst()
    {
        var notes = Announcement.ReleaseNotes(
            [Package("Package.Y", "9.0.0", "Added a feature."), Package("Package.X", "1.0.0", "Fixed a bug.")],
            "Package.X",
            RunUrl,
            "  A big release!\n");

        Assert.AreEqual("A big release!\n\nFixed a bug.\n\n### Package.Y 9.0.0\n\nAdded a feature.\n\n" + Footer, notes);
    }

    [TestMethod]
    public void ReleaseNotes_MissingNotesArePlaceholder()
    {
        var notes = Announcement.ReleaseNotes([Package("A", "1.0.0")], "A", RunUrl);

        Assert.AreEqual("*No release notes provided.*\n\n" + Footer, notes);
    }

    [TestMethod]
    public void SelectMainPackage_WithoutMainProjectUsesHighestVersion()
    {
        PackageEntry[] packages = [Package("A", "2.0.0"), Package("B", "10.0.0-beta"), Package("C", "9.0.0")];

        Assert.AreEqual("B", Announcement.SelectMainPackage(packages, "").Id);
    }

    [TestMethod]
    public void SelectMainPackage_ReleaseIsHigherThanPrerelease()
    {
        PackageEntry[] packages = [Package("Beta", "2.0.0-beta"), Package("Release", "2.0.0")];

        Assert.AreEqual("Release", Announcement.SelectMainPackage(packages, "").Id);
    }

    [TestMethod]
    public void SelectMainPackage_MatchesIdOrProjectDirectory()
    {
        PackageEntry[] packages =
        [
            Package("Company.Core", "1.0.0", project: Path.Combine("src", "Core", "Core.csproj")),
            Package("Company.Extra", "2.0.0", project: Path.Combine("src", "Extra", "Extra.csproj")),
        ];

        Assert.AreEqual("Company.Core", Announcement.SelectMainPackage(packages, "company.core").Id);
        Assert.AreEqual("Company.Core", Announcement.SelectMainPackage(packages, "Core").Id);
        Assert.AreEqual("Company.Extra", Announcement.SelectMainPackage(packages, "Unknown").Id);
    }

    [TestMethod]
    public void DiscordMessage_FormatsPackagesAndLinks()
    {
        PackageEntry[] packages =
        [
            Package("GBX.NET", "2.4.4", "- Added a chunk"),
            Package("GBX.NET.PAK", "2.4.4", "- Fixed blocks"),
            Package("GBX.NET.Crypto", "1.2.2", "- Added MD5"),
        ];
        const string releaseUrl = "https://github.com/owner/repo/releases/tag/v2.4.4";

        var message = Announcement.DiscordMessage(packages, packages[0], releaseUrl, true, "Introduction", "Thanks for using Package.X!");
        var withoutNuGet = Announcement.DiscordMessage(packages, packages[0], releaseUrl, false, "", "");

        Assert.AreEqual(
            "## GBX.NET 2.4.4\n\nIntroduction\n\n- Added a chunk\n\n"
            + "### GBX.NET.PAK 2.4.4\n\n- Fixed blocks\n\n"
            + "### GBX.NET.Crypto 1.2.2\n\n- Added MD5\n\n"
            + $"GitHub: <{releaseUrl}>\n"
            + "NuGet: <https://www.nuget.org/packages/GBX.NET/2.4.4>\n\n"
            + "Thanks for using Package.X!\n",
            message);
        Assert.IsFalse(withoutNuGet.Contains("NuGet:"));
    }
}
