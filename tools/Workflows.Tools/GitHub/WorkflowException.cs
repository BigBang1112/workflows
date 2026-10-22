namespace Workflows.Tools.GitHub;

/// <summary>
/// An expected failure. The message is shown as an error annotation instead of a stack trace.
/// </summary>
internal sealed class WorkflowException(string message) : Exception(message);
