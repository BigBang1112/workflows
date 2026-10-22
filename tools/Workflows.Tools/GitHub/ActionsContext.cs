using System.Collections;

namespace Workflows.Tools.GitHub;

/// <summary>
/// What a step sees on a GitHub Actions runner: environment variables, the working directory,
/// the log, and the files for step outputs and the job summary.
/// </summary>
internal sealed class ActionsContext(IReadOnlyDictionary<string, string> environment, string workingDirectory, TextWriter log)
{
    public string WorkingDirectory { get; } = workingDirectory;

    public TextWriter Log { get; } = log;

    public static ActionsContext FromProcess()
    {
        var environment = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? "");

        return new ActionsContext(environment, Environment.CurrentDirectory, Console.Out);
    }

    /// <summary>Gets a variable, or an empty string when it isn't set.</summary>
    public string Get(string name) => environment.GetValueOrDefault(name, "");

    public string Require(string name)
    {
        var value = Get(name);
        return value.Length > 0 ? value : throw new WorkflowException($"{name} is not set");
    }

    /// <summary>Gets a boolean input. Only an unset variable falls back to the default.</summary>
    public bool GetBool(string name, bool defaultValue)
    {
        return environment.TryGetValue(name, out var value)
            ? value.Equals("true", StringComparison.OrdinalIgnoreCase)
            : defaultValue;
    }

    /// <summary>Gets a newline-separated list without blank lines.</summary>
    public IReadOnlyList<string> GetLines(string name)
    {
        return Get(name)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    public string TempPath(string name) => Path.Combine(ResolvePath(Require("RUNNER_TEMP")), name);

    public string ResolvePath(string path) => Path.GetFullPath(path, WorkingDirectory);

    public void SetOutput(string name, string value)
    {
        var path = Get("GITHUB_OUTPUT");
        if (path.Length > 0)
        {
            File.AppendAllText(path, $"{name}={value}\n");
        }
    }

    public void AppendSummary(string markdown)
    {
        var path = Get("GITHUB_STEP_SUMMARY");
        if (path.Length > 0)
        {
            File.AppendAllText(path, markdown);
        }
    }
}
