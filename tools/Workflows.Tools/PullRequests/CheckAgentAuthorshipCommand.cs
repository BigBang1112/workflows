using System.Text;
using System.Text.Json;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.PullRequests;

/// <summary>
/// Checks a pull request for commits authored or co-authored by AI agents.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Co-authored: comments with the commits to rewrite and how.</item>
/// <item>Agent only (opened by an agent, or every commit by one): comments and closes the pull request.</item>
/// <item>None: marks an earlier comment as resolved.</item>
/// </list>
/// The check fails unless the result is none. Outputs <c>result</c>: none, co-authored, or agent-only.
/// </remarks>
internal sealed class CheckAgentAuthorshipCommand(ActionsContext context, GitHubApi github)
{
    public async Task RunAsync()
    {
        var repository = context.Require("GITHUB_REPOSITORY");
        var number = context.Require("PULL_REQUEST");
        var patterns = AgentPatterns.WithDefaults(context.GetLines("AGENT_PATTERNS"));
        var dryRun = context.GetBool("DRY_RUN", false);
        var close = context.GetBool("CLOSE_AGENT_PULL_REQUESTS", true);

        var pullRequest = await github.GetAsync($"repos/{repository}/pulls/{number}");
        var author = pullRequest.GetProperty("user").GetProperty("login").GetString() ?? "";
        var isOpen = pullRequest.GetProperty("state").GetString() == "open";
        var baseBranch = pullRequest.GetProperty("base").GetProperty("ref").GetString() ?? "";

        var commits = (await github.GetAllPagesAsync($"repos/{repository}/pulls/{number}/commits")).Select(ReadCommit).ToList();
        var report = AgentAuthorship.Analyze(author, commits, patterns);

        context.SetOutput("result", report.Result switch
        {
            AgentAuthorshipResult.AgentOnly => "agent-only",
            AgentAuthorshipResult.CoAuthored => "co-authored",
            _ => "none",
        });
        context.AppendSummary(Summary(number, report, commits.Count));

        if (dryRun)
        {
            context.Log.WriteLine("Dry run: no comment was written and the pull request wasn't closed.");
        }
        else
        {
            await RespondAsync(repository, number, report, baseBranch, close && isOpen);
        }

        switch (report.Result)
        {
            case AgentAuthorshipResult.AgentOnly:
                throw new WorkflowException("This pull request was created by an AI agent.");
            case AgentAuthorshipResult.CoAuthored:
                throw new WorkflowException($"{report.AgentCommits.Count} of {commits.Count} commits name an AI agent as author or co-author.");
        }

        context.Log.WriteLine($"No AI agent authorship found in {commits.Count} commits.");
    }

    private async Task RespondAsync(string repository, string number, AgentAuthorshipReport report, string baseBranch, bool close)
    {
        var existing = (await github.GetAllPagesAsync($"repos/{repository}/issues/{number}/comments"))
            .FirstOrDefault(comment => comment.GetProperty("body").GetString()?.StartsWith(AgentComment.Marker) == true);

        var body = report.Result switch
        {
            AgentAuthorshipResult.AgentOnly => AgentComment.AgentOnly(report.AgentPullRequestAuthor, close),
            AgentAuthorshipResult.CoAuthored => AgentComment.CoAuthored(report.AgentCommits, baseBranch),
            _ => existing.ValueKind == JsonValueKind.Undefined ? null : AgentComment.Resolved(),
        };

        if (body is not null)
        {
            if (existing.ValueKind == JsonValueKind.Undefined)
            {
                await github.PostAsync($"repos/{repository}/issues/{number}/comments", new { body });
            }
            else if (existing.GetProperty("body").GetString() != body)
            {
                await github.PatchAsync($"repos/{repository}/issues/comments/{existing.GetProperty("id").GetInt64()}", new { body });
            }
        }

        if (report.Result == AgentAuthorshipResult.AgentOnly && close)
        {
            await github.PatchAsync($"repos/{repository}/pulls/{number}", new { state = "closed" });
            context.Log.WriteLine($"Closed pull request #{number}.");
        }
    }

    private static PullRequestCommit ReadCommit(JsonElement item)
    {
        var commit = item.GetProperty("commit");
        var author = commit.GetProperty("author");
        var login = item.TryGetProperty("author", out var user) && user.ValueKind == JsonValueKind.Object
            ? user.GetProperty("login").GetString() ?? ""
            : "";

        return new PullRequestCommit(
            item.GetProperty("sha").GetString() ?? "",
            commit.GetProperty("message").GetString() ?? "",
            author.GetProperty("name").GetString() ?? "",
            author.GetProperty("email").GetString() ?? "",
            login);
    }

    private static string Summary(string number, AgentAuthorshipReport report, int commitCount)
    {
        var summary = new StringBuilder();
        summary.Append($"## AI agent authorship of #{number}\n\n");

        summary.Append(report.Result switch
        {
            AgentAuthorshipResult.AgentOnly when report.AgentPullRequestAuthor is not null => $"Opened by an AI agent (`{report.AgentPullRequestAuthor}`).\n",
            AgentAuthorshipResult.AgentOnly => "Every commit was authored by an AI agent.\n",
            AgentAuthorshipResult.CoAuthored => $"{report.AgentCommits.Count} of {commitCount} commits name an AI agent.\n",
            _ => $"None of the {commitCount} commits name an AI agent.\n",
        });

        return summary.ToString();
    }
}
