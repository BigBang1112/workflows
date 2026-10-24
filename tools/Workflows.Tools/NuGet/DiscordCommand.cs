using Workflows.Tools.Discord;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.NuGet;

/// <summary>
/// Announces the release of the selected packages on Discord.
/// The NuGet.org link is only included when the main package is on NuGet.org.
/// </summary>
internal sealed class DiscordCommand(ActionsContext context, DiscordWebhook webhook)
{
    private readonly PublishState state = new(context);

    public async Task RunAsync()
    {
        var webhookUrl = context.Require("DISCORD_WEBHOOK_URL");
        var packages = state.ReleasePackages;
        var main = Announcement.SelectMainPackage(packages, context.Get("MAIN_PROJECT"));

        var nugetAvailable = state.Results.Any(result =>
            result.Id == main.Id
            && result.Version == main.Version
            && result.Feeds.GetValueOrDefault(PublishCommand.NuGetOrg) is PushStatus.Uploaded or PushStatus.Duplicate);

        var message = Announcement.DiscordMessage(
            packages,
            main,
            context.Require("RELEASE_URL"),
            nugetAvailable,
            context.Get("DISCORD_TOP_LINES"),
            context.Get("DISCORD_BOTTOM_LINES"));

        await webhook.PostAsync(webhookUrl, message);
    }
}
