using Workflows.Tools.GitHub;

namespace Workflows.Tools.Tests.GitHub;

[TestClass]
public sealed class GitHubReleaseTests
{
    [TestMethod]
    public void Title_SeparatesPrefixFromVersion()
    {
        Assert.AreEqual("Release 1.0.0", GitHubRelease.Title("v1.0.0", "Release"));
        Assert.AreEqual("1.0.0", GitHubRelease.Title("v1.0.0", ""));
        Assert.AreEqual("1.0.0", GitHubRelease.Title("1.0.0", "  "));
    }
}
