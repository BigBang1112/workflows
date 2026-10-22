namespace Workflows.Tools.NuGet;

internal static class PackageVersion
{
    /// <summary>
    /// Normalizes a version the way NuGet writes it into a package: at least three numeric parts,
    /// no fourth part when it is zero, and case-insensitive labels.
    /// </summary>
    public static string Normalize(string version)
    {
        var (core, label) = SplitLabel(version);
        var parts = core.Split('.').Select(part => int.TryParse(part, out var number) ? number.ToString() : part).ToList();

        while (parts.Count < 3)
        {
            parts.Add("0");
        }

        if (parts.Count == 4 && parts[3] == "0")
        {
            parts.RemoveAt(3);
        }

        return (string.Join('.', parts) + label).ToLowerInvariant();
    }

    /// <summary>
    /// Orders versions by their numeric parts. A release is higher than its prereleases,
    /// and prereleases are ordered by their label. Build metadata is ignored.
    /// </summary>
    public static int Compare(string left, string right)
    {
        var (leftNumbers, leftPrerelease) = Parse(left);
        var (rightNumbers, rightPrerelease) = Parse(right);

        for (var i = 0; i < Math.Min(leftNumbers.Length, rightNumbers.Length); i++)
        {
            if (leftNumbers[i] != rightNumbers[i])
            {
                return leftNumbers[i].CompareTo(rightNumbers[i]);
            }
        }

        if (leftNumbers.Length != rightNumbers.Length)
        {
            return leftNumbers.Length.CompareTo(rightNumbers.Length);
        }

        return (leftPrerelease.Length == 0, rightPrerelease.Length == 0) switch
        {
            (true, false) => 1,
            (false, true) => -1,
            _ => string.CompareOrdinal(leftPrerelease, rightPrerelease),
        };
    }

    private static (int[] Numbers, string Prerelease) Parse(string version)
    {
        var withoutMetadata = version.Split('+', 2)[0];
        var parts = withoutMetadata.Split('-', 2);
        return (parts[0].Split('.').Select(int.Parse).ToArray(), parts.Length > 1 ? parts[1] : "");
    }

    private static (string Core, string Label) SplitLabel(string version)
    {
        var index = version.IndexOfAny(['-', '+']);
        return index < 0 ? (version, "") : (version[..index], version[index..]);
    }
}
