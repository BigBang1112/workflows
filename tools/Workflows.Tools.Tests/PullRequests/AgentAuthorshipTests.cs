using Workflows.Tools.PullRequests;

namespace Workflows.Tools.Tests.PullRequests;

[TestClass]
public sealed class AgentAuthorshipTests
{
    private static readonly AgentPatterns Patterns = AgentPatterns.WithDefaults([]);

    private static PullRequestCommit Human(string message = "Fix bug") => new("1111111aaaa", message, "Jane Doe", "jane@example.com", "jane");

    // Identities taken from real commits and pull requests.
    [TestMethod]
    [DataRow("Claude Opus 5.5 <noreply@anthropic.com>")]
    [DataRow("Copilot <copilot@github.com>")]
    [DataRow("Copilot")]
    [DataRow("copilot-swe-agent[bot] <198982749+Copilot@users.noreply.github.com>")]
    [DataRow("Devin AI <158243242+devin-ai-integration[bot]@users.noreply.github.com>")]
    [DataRow("devin-ai-integration[bot]")]
    [DataRow("claude[bot]")]
    [DataRow("google-labs-jules[bot]")]
    [DataRow("Cursor Agent <cursoragent@cursor.com>")]
    [DataRow("Jane Doe (aider) <jane@example.com>")]
    public void Defaults_RecognizeAgents(string identity)
    {
        Assert.IsTrue(Patterns.IsAgent(identity));
    }

    [TestMethod]
    [DataRow("Jane Doe <jane@example.com>")]
    [DataRow("Claude Monet <claude@example.com>")]
    [DataRow("dependabot[bot]")]
    [DataRow("Copilot Fan <copilotfan@example.com>")]
    [DataRow("")]
    public void Defaults_IgnorePeopleAndOtherBots(string identity)
    {
        Assert.IsFalse(Patterns.IsAgent(identity));
    }

    [TestMethod]
    public void ExtraPatterns_AddToDefaults()
    {
        var patterns = AgentPatterns.WithDefaults([@"my-agent\[bot\]"]);

        Assert.IsTrue(patterns.IsAgent("my-agent[bot]"));
        Assert.IsTrue(patterns.IsAgent("Copilot"));
    }

    [TestMethod]
    public void CoAuthors_ReadsEveryTrailerCaseInsensitively()
    {
        var coAuthors = AgentAuthorship.CoAuthors("Fix bug\r\n\r\nCo-authored-by: Jane <jane@example.com>\r\nCO-AUTHORED-BY:   Claude <noreply@anthropic.com>  \r\n");

        CollectionAssert.AreEqual(new[] { "Jane <jane@example.com>", "Claude <noreply@anthropic.com>" }, coAuthors.ToList());
    }

    [TestMethod]
    public void Analyze_NoAgent()
    {
        var report = AgentAuthorship.Analyze("jane", [Human(), Human("Fix\n\nCo-authored-by: Bob <bob@example.com>")], Patterns);

        Assert.AreEqual(AgentAuthorshipResult.None, report.Result);
        Assert.AreEqual(0, report.AgentCommits.Count);
    }

    [TestMethod]
    public void Analyze_AgentCoAuthorInSomeCommits()
    {
        var report = AgentAuthorship.Analyze("jane", [Human(), Human("Add feature\n\nCo-Authored-By: Claude <noreply@anthropic.com>")], Patterns);

        Assert.AreEqual(AgentAuthorshipResult.CoAuthored, report.Result);
        CollectionAssert.AreEqual(new[] { "Claude <noreply@anthropic.com>" }, report.AgentCommits.Single().AgentCoAuthors.ToList());
        Assert.IsNull(report.AgentPullRequestAuthor);
    }

    [TestMethod]
    public void Analyze_AgentAuthorOfSomeCommitsIsCoAuthored()
    {
        var agentCommit = new PullRequestCommit("2222222bbbb", "Refactor", "Cursor Agent", "cursoragent@cursor.com", "");

        var report = AgentAuthorship.Analyze("jane", [Human(), agentCommit], Patterns);

        Assert.AreEqual(AgentAuthorshipResult.CoAuthored, report.Result);
        Assert.AreEqual("Cursor Agent <cursoragent@cursor.com>", report.AgentCommits.Single().AgentAuthor);
    }

    [TestMethod]
    public void Analyze_PullRequestOpenedByAgentIsAgentOnly()
    {
        var report = AgentAuthorship.Analyze("Copilot", [Human()], Patterns);

        Assert.AreEqual(AgentAuthorshipResult.AgentOnly, report.Result);
        Assert.AreEqual("Copilot", report.AgentPullRequestAuthor);
    }

    [TestMethod]
    public void Analyze_EveryCommitByAgentIsAgentOnly()
    {
        var commit = new PullRequestCommit("3333333cccc", "Initial plan", "copilot-swe-agent[bot]", "198982749+Copilot@users.noreply.github.com", "Copilot");

        var report = AgentAuthorship.Analyze("jane", [commit], Patterns);

        Assert.AreEqual(AgentAuthorshipResult.AgentOnly, report.Result);
        Assert.IsNull(report.AgentPullRequestAuthor);
    }
}
