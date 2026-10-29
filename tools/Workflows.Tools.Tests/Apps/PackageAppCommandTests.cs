using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Workflows.Tools.Apps;
using Workflows.Tools.GitHub;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.Apps;

[TestClass]
public sealed class PackageAppCommandTests
{
    private const UnixFileMode Executable = (UnixFileMode)0b111_101_101;
    private const UnixFileMode Regular = (UnixFileMode)0b110_100_100;

    private static TestRunner CreateRunner()
    {
        var runner = new TestRunner();
        runner.Environment["ASSET_NAME"] = "App";
        return runner;
    }

    /// <summary>Writes a build the way the workflow does, with "tar -cf ../[runner].tar ." in the published output.</summary>
    private static void WriteBuild(TestRunner runner, string name, params (string Path, UnixFileMode Mode)[] files)
    {
        var builds = new AppPaths(runner.Context).Builds;
        Directory.CreateDirectory(builds);

        using var writer = new TarWriter(File.Create(Path.Combine(builds, $"{name}.tar")), TarEntryFormat.Gnu);
        writer.WriteEntry(new GnuTarEntry(TarEntryType.Directory, "./"));

        foreach (var (path, mode) in files)
        {
            writer.WriteEntry(new GnuTarEntry(TarEntryType.RegularFile, $"./{path}")
            {
                Mode = mode,
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes($"{name}/{path}")),
            });
        }
    }

    private static ZipArchive OpenAsset(TestRunner runner, string name) => ZipFile.OpenRead(Path.Combine(new AppPaths(runner.Context).Assets, name));

    private static List<string> Entries(ZipArchive archive) => archive.Entries.Select(entry => entry.FullName).ToList();

    [TestMethod]
    public async Task Run_CreatesOneZipPerRunnerWithPackageDirectory()
    {
        using var runner = CreateRunner();
        runner.Environment["PACKAGE_DIRECTORY"] = "Plugin";
        runner.WriteFile("Plugin/manifest.toml");
        runner.WriteFile("Plugin/scripts/import.py");
        WriteBuild(runner, "Linux-X64", ("App", Executable), ("App.dll", Regular), ("runtimes/native.so", Regular));
        WriteBuild(runner, "Windows-X64", ("App.exe", Regular), ("App.dll", Regular));

        await new PackageAppCommand(runner.Context).RunAsync();

        using (var linux = OpenAsset(runner, "App-linux-x64.zip"))
        {
            CollectionAssert.AreEqual(
                new[] { "App", "App.dll", "manifest.toml", "runtimes/native.so", "scripts/import.py" },
                Entries(linux));
            Assert.AreEqual((0x8000 | (int)Executable) << 16, linux.GetEntry("App")!.ExternalAttributes);
            Assert.AreEqual((0x8000 | (int)Regular) << 16, linux.GetEntry("App.dll")!.ExternalAttributes);
            Assert.AreEqual("Linux-X64/App", new StreamReader(linux.GetEntry("App")!.Open()).ReadToEnd());
        }

        using (var windows = OpenAsset(runner, "App-windows-x64.zip"))
        {
            CollectionAssert.AreEqual(new[] { "App.dll", "App.exe", "manifest.toml", "scripts/import.py" }, Entries(windows));
        }

        StringAssert.Contains(runner.Summary, "| App-linux-x64.zip | 5 | `");
        StringAssert.Contains(runner.Summary, "| App-windows-x64.zip | 4 | `");
    }

    [TestMethod]
    public async Task Run_MergesSelectedFilesOfEveryRunnerIntoOneZip()
    {
        using var runner = CreateRunner();
        runner.Environment["MERGE_RUNNERS"] = "true";
        runner.Environment["FILES"] = "App\nApp.exe\n";
        runner.Environment["PACKAGE_DIRECTORY"] = "Plugin";
        runner.WriteFile("Plugin/__init__.py");
        WriteBuild(runner, "Linux-X64", ("App", Executable), ("App.dbg", Regular));
        WriteBuild(runner, "Windows-X64", ("App.exe", Regular), ("App.pdb", Regular));

        await new PackageAppCommand(runner.Context).RunAsync();

        using var asset = OpenAsset(runner, "App.zip");
        CollectionAssert.AreEqual(new[] { "App", "App.exe", "__init__.py" }, Entries(asset));
        Assert.AreEqual(1, Directory.GetFiles(new AppPaths(runner.Context).Assets).Length);
    }

    [TestMethod]
    public async Task Run_NormalizesModesFromWindowsTar()
    {
        using var runner = CreateRunner();
        WriteBuild(runner, "Windows-X64", ("App.exe", (UnixFileMode)0b111_000_000), ("App.dll", (UnixFileMode)0b110_000_000));

        await new PackageAppCommand(runner.Context).RunAsync();

        using var asset = OpenAsset(runner, "App-windows-x64.zip");
        Assert.AreEqual((0x8000 | (int)Executable) << 16, asset.GetEntry("App.exe")!.ExternalAttributes);
        Assert.AreEqual((0x8000 | (int)Regular) << 16, asset.GetEntry("App.dll")!.ExternalAttributes);
    }

    [TestMethod]
    public async Task Run_FailsWhenMergedRunnersHaveTheSamePath()
    {
        using var runner = CreateRunner();
        runner.Environment["MERGE_RUNNERS"] = "true";
        WriteBuild(runner, "Linux-X64", ("App", Executable), ("App.dll", Regular));
        WriteBuild(runner, "Windows-X64", ("App.exe", Regular), ("app.dll", Regular));

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new PackageAppCommand(runner.Context).RunAsync());

        Assert.AreEqual("App.zip would contain App.dll from both the linux-x64 build and the windows-x64 build", exception.Message);
    }

    [TestMethod]
    public async Task Run_FailsWhenPackageDirectoryHasAPublishedPath()
    {
        using var runner = CreateRunner();
        runner.Environment["PACKAGE_DIRECTORY"] = "Plugin";
        runner.WriteFile("Plugin/App");
        WriteBuild(runner, "Linux-X64", ("App", Executable));

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new PackageAppCommand(runner.Context).RunAsync());

        Assert.AreEqual("App-linux-x64.zip would contain App from both the package directory and the linux-x64 build", exception.Message);
    }

    [TestMethod]
    public async Task Run_FailsWhenPatternMatchesNoFile()
    {
        using var runner = CreateRunner();
        runner.Environment["FILES"] = "App\nMissing.exe";
        WriteBuild(runner, "Linux-X64", ("App", Executable));

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new PackageAppCommand(runner.Context).RunAsync());

        Assert.AreEqual("No published files match: Missing.exe", exception.Message);
    }

    [TestMethod]
    public async Task Run_FailsWhenRunnerKeepsNoFile()
    {
        using var runner = CreateRunner();
        runner.Environment["FILES"] = "*.exe";
        WriteBuild(runner, "Linux-X64", ("App", Executable));
        WriteBuild(runner, "Windows-X64", ("App.exe", Regular));

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new PackageAppCommand(runner.Context).RunAsync());

        Assert.AreEqual("No published files of the linux-x64 build match the files input", exception.Message);
    }

    [TestMethod]
    public async Task Run_FailsWithoutPackageDirectory()
    {
        using var runner = CreateRunner();
        runner.Environment["PACKAGE_DIRECTORY"] = "Missing";
        WriteBuild(runner, "Linux-X64", ("App", Executable));

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new PackageAppCommand(runner.Context).RunAsync());

        Assert.AreEqual("Package directory not found: Missing", exception.Message);
    }

    [TestMethod]
    public async Task Run_FailsWithoutBuilds()
    {
        using var runner = CreateRunner();

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new PackageAppCommand(runner.Context).RunAsync());

        StringAssert.StartsWith(exception.Message, "No builds found in ");
    }
}
