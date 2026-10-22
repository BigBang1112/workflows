using System.Text;

namespace Workflows.Tools.PullRequests;

/// <summary>
/// The comment that explains what to change. The workflow keeps a single comment per pull request,
/// found by <see cref="Marker"/>, and updates it on every run.
/// </summary>
internal static class AgentComment
{
    public const string Marker = "<!-- check-agent-authorship -->";

    public static string AgentOnly(string? agentLogin, bool closed)
    {
        var who = agentLogin is null
            ? "Every commit in this pull request was authored by an AI agent"
            : $"This pull request was opened by an AI agent (`{agentLogin}`)";

        var body = new StringBuilder();
        body.Append($"{Marker}\n");
        body.Append(closed ? "### Closed: created by an AI agent\n\n" : "### Created by an AI agent\n\n");
        body.Append($"{who}. This repository only accepts pull requests made by people");
        body.Append(closed ? ", so it was closed automatically.\n\n" : ".\n\n");
        body.Append("If you made these changes yourself with help from an agent, open a new pull request from your own account, with commits that have you as the author and no agent co-authors.\n");
        return body.ToString();
    }

    public static string CoAuthored(IReadOnlyList<AgentCommit> commits, string baseBranch)
    {
        var body = new StringBuilder();
        body.Append($"{Marker}\n");
        body.Append("### AI agent co-authors\n\n");
        body.Append("Some commits in this pull request name an AI agent as their author or co-author. Please remove the agent from these commits:\n\n");
        body.Append("| Commit | Agent |\n");
        body.Append("|---|---|\n");

        foreach (var commit in commits)
        {
            var agents = new List<string>();

            if (commit.AgentAuthor is not null)
            {
                agents.Add($"Author: `{commit.AgentAuthor}`");
            }

            agents.AddRange(commit.AgentCoAuthors.Select(coAuthor => $"Co-authored-by: `{coAuthor}`"));
            body.Append($"| {commit.Commit.Sha[..7]} {EscapeCell(commit.Commit.Subject)} | {string.Join("<br>", agents)} |\n");
        }

        body.Append($"""

            To rewrite the commits:

            1. Run `git rebase -i origin/{baseBranch}`.
            2. Change `pick` to `edit` for each commit listed above, then save and close the editor.
            3. For each commit, run `git commit --amend --reset-author`, delete the agent's `Co-authored-by` line from the message, and run `git rebase --continue`. `--reset-author` makes you the author.
            4. Run `git push --force-with-lease`.

            When only the last commit is affected, `git commit --amend --reset-author` and `git push --force-with-lease` are enough.

            This check runs again after you push.

            """);

        return body.ToString().ReplaceLineEndings("\n");
    }

    public static string Resolved()
    {
        return $"{Marker}\n### AI agent co-authors\n\nResolved. No commit in this pull request names an AI agent as its author or co-author anymore.\n";
    }

    private static string EscapeCell(string text) => text.Replace("|", "\\|");
}
