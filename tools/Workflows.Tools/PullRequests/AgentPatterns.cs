using System.Text.RegularExpressions;

namespace Workflows.Tools.PullRequests;

/// <summary>
/// Recognizes AI agents by their GitHub login, or by the name and email in a commit author or
/// Co-authored-by trailer ("Name &lt;email&gt;"). Patterns are case-insensitive regular expressions.
/// </summary>
internal sealed class AgentPatterns(IEnumerable<string> patterns)
{
    /// <summary>Identities that AI agents are known to use.</summary>
    public static readonly IReadOnlyList<string> Defaults =
    [
        // Claude Code adds this co-author, and the Claude GitHub app commits and opens pull requests as claude[bot].
        @"noreply@anthropic\.com",
        @"claude\[bot\]",

        // GitHub Copilot in editors adds the first co-author. The Copilot coding agent opens pull requests
        // as "Copilot" and commits as copilot-swe-agent[bot].
        @"copilot@github\.com",
        @"^Copilot$",
        @"copilot-swe-agent\[bot\]",
        @"\+Copilot@users\.noreply\.github\.com",

        @"devin-ai-integration\[bot\]",
        @"google-labs-jules\[bot\]",
        @"cursoragent@cursor\.com",

        // aider adds "(aider)" to the commit author name.
        @"\(aider\)",
    ];

    private readonly List<Regex> regexes = patterns
        .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        .ToList();

    public static AgentPatterns WithDefaults(IEnumerable<string> extraPatterns) => new([.. Defaults, .. extraPatterns]);

    public bool IsAgent(string identity) => identity.Length > 0 && regexes.Any(regex => regex.IsMatch(identity));
}
