using System.Text.Json;
using Workflows.Tools.GitHub;
using Workflows.Tools.PullRequests;
using Workflows.Tools.Tests.Fakes;

namespace Workflows.Tools.Tests.PullRequests;

[TestClass]
public sealed class CheckAgentAuthorshipCommandTests
{
    private const string ClaudeCoAuthored = "Add feature\n\nCo-Authored-By: Claude <noreply@anthropic.com>";

    private static TestRunner CreateRunner()
    {
        var runner = new TestRunner();
        runner.Environment["PULL_REQUEST"] = "7";
        return runner;
    }

    private static string Body(string write) => JsonDocument.Parse(write[(write.IndexOf(' ', write.IndexOf(' ') + 1) + 1)..]).RootElement.GetProperty("body").GetString()!;

    [TestMethod]
    public async Task NoAgent_PassesWithoutWriting()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub();
        github.AddCommit("1111111aaaa", "Fix bug");

        await new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync();

        Assert.AreEqual(0, github.Writes.Count());
        StringAssert.Contains(runner.Output, "result=none");
    }

    [TestMethod]
    public async Task CoAuthored_CommentsAndFails()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub();
        github.AddCommit("1111111aaaa", "Fix bug");
        github.AddCommit("2222222bbbb", ClaudeCoAuthored);

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        Assert.AreEqual("1 of 2 commits name an AI agent as author or co-author.", exception.Message);
        StringAssert.Contains(runner.Output, "result=co-authored");

        var write = github.Writes.Single();
        StringAssert.StartsWith(write, "POST /repos/owner/repo/issues/7/comments ");
        var body = Body(write);
        StringAssert.StartsWith(body, AgentComment.Marker);
        StringAssert.Contains(body, "| 2222222 Add feature | Co-authored-by: `Claude <noreply@anthropic.com>` |");
        StringAssert.Contains(body, "git rebase -i origin/main");
        Assert.IsFalse(body.Contains("1111111"));
    }

    [TestMethod]
    public async Task CoAuthored_UpdatesExistingComment()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub();
        github.AddCommit("2222222bbbb", ClaudeCoAuthored);
        github.Comments.Add(new { id = 99, body = "Thanks!" });
        github.Comments.Add(new { id = 100, body = AgentComment.Marker + "\nOld text" });

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        StringAssert.StartsWith(github.Writes.Single(), "PATCH /repos/owner/repo/issues/comments/100 ");
    }

    [TestMethod]
    public async Task CoAuthored_LeavesUnchangedCommentAlone()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub();
        github.AddCommit("2222222bbbb", ClaudeCoAuthored);

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());
        github.Comments.Add(new { id = 100, body = Body(github.Writes.Single()) });
        github.Handler.Requests.Clear();

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        Assert.AreEqual(0, github.Writes.Count());
    }

    [TestMethod]
    public async Task AgentOnly_CommentsAndClosesPullRequest()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub { Author = "Copilot" };
        github.AddCommit("3333333cccc", "Initial plan", "copilot-swe-agent[bot]", "198982749+Copilot@users.noreply.github.com", "Copilot");

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        var writes = github.Writes.ToList();
        Assert.AreEqual(2, writes.Count);
        StringAssert.Contains(Body(writes[0]), "### Closed: created by an AI agent");
        StringAssert.Contains(Body(writes[0]), "opened by an AI agent (`Copilot`)");
        Assert.AreEqual("""PATCH /repos/owner/repo/pulls/7 {"state":"closed"}""", writes[1]);
        StringAssert.Contains(runner.Output, "result=agent-only");
    }

    [TestMethod]
    public async Task AgentOnly_WithoutClosingOnlyComments()
    {
        using var runner = CreateRunner();
        runner.Environment["CLOSE_AGENT_PULL_REQUESTS"] = "false";
        var github = new FakeGitHub { Author = "devin-ai-integration[bot]" };

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        var body = Body(github.Writes.Single());
        StringAssert.Contains(body, "### Created by an AI agent");
        Assert.IsFalse(body.Contains("closed"));
    }

    [TestMethod]
    public async Task AgentOnly_DoesNotCloseClosedPullRequest()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub { Author = "Copilot", State = "closed" };

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        Assert.IsFalse(github.Writes.Any(write => write.Contains("/pulls/7")));
    }

    [TestMethod]
    public async Task Resolved_UpdatesEarlierComment()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub();
        github.AddCommit("1111111aaaa", "Fix bug");
        github.Comments.Add(new { id = 100, body = AgentComment.Marker + "\nOld text" });

        await new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync();

        var write = github.Writes.Single();
        StringAssert.StartsWith(write, "PATCH /repos/owner/repo/issues/comments/100 ");
        StringAssert.Contains(Body(write), "Resolved.");
    }

    [TestMethod]
    public async Task DryRun_DoesNotWrite()
    {
        using var runner = CreateRunner();
        runner.Environment["DRY_RUN"] = "true";
        var github = new FakeGitHub { Author = "Copilot" };

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        Assert.AreEqual(0, github.Writes.Count());
        StringAssert.Contains(runner.Output, "result=agent-only");
    }

    [TestMethod]
    public async Task CustomPatterns_AreUsed()
    {
        using var runner = CreateRunner();
        runner.Environment["AGENT_PATTERNS"] = "my-agent@example\\.com";
        var github = new FakeGitHub();
        github.AddCommit("1111111aaaa", "Fix bug");
        github.AddCommit("2222222bbbb", "Add feature\n\nCo-authored-by: Helper <my-agent@example.com>");

        await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        StringAssert.Contains(runner.Output, "result=co-authored");
    }

    [TestMethod]
    public async Task Api_SendsRequiredHeaders()
    {
        using var runner = CreateRunner();
        var github = new FakeGitHub();

        await new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync();

        var request = github.Handler.Requests[0].Request;
        Assert.AreEqual("Bearer token", request.Headers.Authorization?.ToString());
        Assert.IsGreaterThan(0, request.Headers.UserAgent.Count);
        Assert.AreEqual("application/vnd.github+json", request.Headers.Accept.Single().MediaType);
    }

    [TestMethod]
    public async Task Api_ReportsGitHubError()
    {
        using var runner = CreateRunner();
        runner.Environment["PULL_REQUEST"] = "404";
        var github = new FakeGitHub();

        var exception = await Assert.ThrowsAsync<WorkflowException>(() => new CheckAgentAuthorshipCommand(runner.Context, github.Api).RunAsync());

        Assert.AreEqual("GitHub API request GET repos/owner/repo/pulls/404 failed with HTTP 404: Not Found", exception.Message);
    }
}
