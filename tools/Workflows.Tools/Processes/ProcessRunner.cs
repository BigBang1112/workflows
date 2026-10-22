using System.Diagnostics;
using System.Text;

namespace Workflows.Tools.Processes;

internal sealed class ProcessRunner(string workingDirectory, TextWriter log) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, bool echo = true)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) => Collect(output, e.Data);
        process.ErrorDataReceived += (_, e) => Collect(error, e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());

        void Collect(StringBuilder builder, string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (builder)
            {
                builder.AppendLine(line);
            }

            if (echo)
            {
                lock (log)
                {
                    log.WriteLine(line);
                }
            }
        }
    }
}
