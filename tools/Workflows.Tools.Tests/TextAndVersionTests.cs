using Workflows.Tools.NuGet;

namespace Workflows.Tools.Tests;

[TestClass]
public sealed class TextAndVersionTests
{
    [TestMethod]
    public void Dedent_RemovesSharedIndentationAndEmptiesBlankLines()
    {
        Assert.AreEqual("\nFirst\n  Nested\n\nLast\n", Text.Dedent("\r\n    First\r\n      Nested\r\n   \r\n    Last\r\n"));
    }

    [TestMethod]
    public void Dedent_KeepsTextWithoutSharedIndentation()
    {
        Assert.AreEqual("First\n  Second", Text.Dedent("First\n  Second"));
    }

    [TestMethod]
    [DataRow("1.2", "1.2.0")]
    [DataRow("1.2.3.0", "1.2.3")]
    [DataRow("1.2.3.4", "1.2.3.4")]
    [DataRow("01.2.3-Beta.1", "1.2.3-beta.1")]
    public void Normalize_MatchesNuGetVersionFormat(string version, string expected)
    {
        Assert.AreEqual(expected, PackageVersion.Normalize(version));
    }

    [TestMethod]
    [DataRow("1.10.0", "1.9.0", 1)]
    [DataRow("2.0.0", "2.0.0-rc.1", 1)]
    [DataRow("2.0.0-alpha", "2.0.0-beta", -1)]
    [DataRow("1.0.0+build.5", "1.0.0", 0)]
    [DataRow("1.0", "1.0.0", -1)]
    public void Compare_OrdersVersions(string left, string right, int expected)
    {
        Assert.AreEqual(expected, Math.Sign(PackageVersion.Compare(left, right)));
    }
}
