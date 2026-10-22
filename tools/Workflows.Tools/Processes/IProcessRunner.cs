namespace Workflows.Tools.Processes;

internal interface IProcessRunner
{
    /// <summary>
    /// Runs a command in the working directory and waits for it to exit.
    /// With <paramref name="echo"/>, the output is also written to the log while the command runs.
    /// </summary>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, bool echo = true);
}

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => StandardOutput + "\n" + StandardError;
}
