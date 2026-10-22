using System.Text.RegularExpressions;

namespace Workflows.Tools.PullRequests;

internal enum AgentAuthorshipResult
{
    /// <summary>No AI agent authored or co-authored anything.</summary>
    None,

    /// <summary>Some commits are authored or co-authored by an AI agent.</summary>
    CoAuthored,

    /// <summary>An AI agent opened the pull request, or authored every commit.</summary>
    AgentOnly,
}

internal sealed record PullRequestCommit(string Sha, string Message, string AuthorName, string AuthorEmail, string AuthorLogin)
{
    public string Subject => Message.Split('\n', 2)[0].Trim();
}

/// <summary>A commit with the agents that authored or co-authored it.</summary>
internal sealed record AgentCommit(PullRequestCommit Commit, string? AgentAuthor, IReadOnlyList<string> AgentCoAuthors);

internal sealed record AgentAuthorshipReport(AgentAuthorshipResult Result, string? AgentPullRequestAuthor, IReadOnlyList<AgentCommit> AgentCommits);

internal static partial class AgentAuthorship
{
    public static AgentAuthorshipReport Analyze(string pullRequestAuthor, IReadOnlyList<PullRequestCommit> commits, AgentPatterns patterns)
    {
        var agentCommits = commits
            .Select(commit => new AgentCommit(
                commit,
                AgentAuthorOf(commit, patterns),
                CoAuthors(commit.Message).Where(patterns.IsAgent).ToList()))
            .Where(commit => commit.AgentAuthor is not null || commit.AgentCoAuthors.Count > 0)
            .ToList();

        var openedByAgent = patterns.IsAgent(pullRequestAuthor);
        var everyCommitByAgent = commits.Count > 0 && agentCommits.Count(commit => commit.AgentAuthor is not null) == commits.Count;

        var result = openedByAgent || everyCommitByAgent ? AgentAuthorshipResult.AgentOnly
            : agentCommits.Count > 0 ? AgentAuthorshipResult.CoAuthored
            : AgentAuthorshipResult.None;

        return new AgentAuthorshipReport(result, openedByAgent ? pullRequestAuthor : null, agentCommits);
    }

    /// <summary>Gets the values of all Co-authored-by trailers. Git compares trailer keys case-insensitively.</summary>
    public static IReadOnlyList<string> CoAuthors(string message)
    {
        return CoAuthorTrailer().Matches(message).Select(match => match.Groups[1].Value.Trim()).ToList();
    }

    private static string? AgentAuthorOf(PullRequestCommit commit, AgentPatterns patterns)
    {
        var author = $"{commit.AuthorName} <{commit.AuthorEmail}>";

        if (patterns.IsAgent(author))
        {
            return author;
        }

        return patterns.IsAgent(commit.AuthorLogin) ? commit.AuthorLogin : null;
    }

    [GeneratedRegex(@"^co-authored-by:[ \t]*(.+?)[ \t]*\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CoAuthorTrailer();
}
