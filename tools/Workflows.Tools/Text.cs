namespace Workflows.Tools;

internal static class Text
{
    /// <summary>
    /// Removes the indentation shared by all non-blank lines, like Python's textwrap.dedent.
    /// MSBuild properties written over several lines in a project file keep the file's indentation.
    /// Lines with only whitespace become empty.
    /// </summary>
    public static string Dedent(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        string? common = null;

        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var indent = line[..(line.Length - line.TrimStart(' ', '\t').Length)];
            common = common is null ? indent : CommonPrefix(common, indent);
        }

        return string.Join('\n', lines.Select(line =>
            string.IsNullOrWhiteSpace(line) ? "" : line[(common?.Length ?? 0)..]));
    }

    private static string CommonPrefix(string left, string right)
    {
        var length = 0;

        while (length < left.Length && length < right.Length && left[length] == right[length])
        {
            length++;
        }

        return left[..length];
    }
}
