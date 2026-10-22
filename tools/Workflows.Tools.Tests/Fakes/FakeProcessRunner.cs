using Workflows.Tools.Processes;

namespace Workflows.Tools.Tests.Fakes;

/// <summary>
/// Records every command and answers with a configured result instead of starting a process.
/// </summary>
internal sealed class FakeProcessRunner(Func<string, IReadOnlyList<string>, ProcessResult>? handler = null) : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    public IEnumerable<string> CommandLines => Calls.Select(call => string.Join(' ', [call.FileName, .. call.Arguments]));

    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, bool echo = true)
    {
        Calls.Add((fileName, arguments.ToList()));
        return Task.FromResult(handler?.Invoke(fileName, arguments) ?? Success());
    }

    public static ProcessResult Success(string output = "") => new(0, output, "");

    public static ProcessResult Failure(string error = "") => new(1, "", error);
}
