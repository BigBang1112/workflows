using Workflows.Tools.Apps;
using Workflows.Tools.Discord;
using Workflows.Tools.GitHub;
using Workflows.Tools.NuGet;
using Workflows.Tools.Processes;
using Workflows.Tools.PullRequests;

var context = ActionsContext.FromProcess();
var processes = new ProcessRunner(context.WorkingDirectory, context.Log);
using var http = new HttpClient();

try
{
    Task? command = args switch
    {
        ["nuget", "build"] => new BuildCommand(context, processes).RunAsync(),
        ["nuget", "test"] => new TestCommand(context, processes).RunAsync(),
        ["nuget", "pack"] => new PackCommand(context, processes).RunAsync(),
        ["nuget", "publish"] => new PublishCommand(context, processes).RunAsync(),
        ["nuget", "release"] => new ReleaseCommand(context, processes).RunAsync(),
        ["nuget", "discord"] => new DiscordCommand(context, new DiscordWebhook(http)).RunAsync(),
        ["app", "package"] => new PackageAppCommand(context).RunAsync(),
        ["app", "release"] => new ReleaseAppCommand(context, processes, GitHubApi.FromContext(context, http)).RunAsync(),
        ["pull-request", "check-agent-authorship"] => new CheckAgentAuthorshipCommand(context, GitHubApi.FromContext(context, http)).RunAsync(),
        _ => null,
    };

    if (command is null)
    {
        Console.Error.WriteLine("""
            Usage:
              Workflows.Tools nuget <build|test|pack|publish|release|discord>
              Workflows.Tools app <package|release>
              Workflows.Tools pull-request check-agent-authorship
            """);
        return 2;
    }

    await command;
    return 0;
}
catch (WorkflowException exception)
{
    Console.WriteLine($"::error::{exception.Message}");
    return 1;
}
