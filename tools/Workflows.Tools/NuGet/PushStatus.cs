using System.Text.RegularExpressions;

namespace Workflows.Tools.NuGet;

internal enum PushStatus
{
    /// <summary>The feed confirmed that it accepted a new package.</summary>
    Uploaded,

    /// <summary>The feed already has this package version.</summary>
    Duplicate,

    /// <summary>The push failed.</summary>
    Failed,

    /// <summary>The push succeeded, but the output doesn't confirm a new upload.</summary>
    Unconfirmed,
}

internal static partial class PushOutput
{
    public static PushStatus Classify(int exitCode, string output)
    {
        if (exitCode != 0)
        {
            return PushStatus.Failed;
        }

        if (Duplicate().IsMatch(output))
        {
            return PushStatus.Duplicate;
        }

        return Pushed().IsMatch(output) || Created().IsMatch(output) ? PushStatus.Uploaded : PushStatus.Unconfirmed;
    }

    public static bool IsProblem(this PushStatus status) => status is PushStatus.Failed or PushStatus.Unconfirmed;

    public static string ToDisplayString(this PushStatus status) => status.ToString().ToLowerInvariant();

    [GeneratedRegex(@"^\s*Your package was pushed\.\s*$", RegexOptions.Multiline)]
    private static partial Regex Pushed();

    [GeneratedRegex(@"^\s*Created\s+https?://", RegexOptions.Multiline)]
    private static partial Regex Created();

    [GeneratedRegex(@"\b(?:already exists|409|conflict)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Duplicate();
}
