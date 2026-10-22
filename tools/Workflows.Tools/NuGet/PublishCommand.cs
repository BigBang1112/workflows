using System.Text;
using Workflows.Tools.GitHub;
using Workflows.Tools.Processes;

namespace Workflows.Tools.NuGet;

internal sealed record Feed(string Name, string Url, string ApiKey);

/// <summary>
/// Pushes every prepared package to the selected feeds and its symbols to NuGet.org.
/// A package counts as newly uploaded when at least one feed confirmed a new upload.
/// A symbols-only upload doesn't count.
/// </summary>
/// <remarks>
/// Outputs <c>has-new</c> and <c>has-failures</c>, and writes a table of every result to the job summary.
/// Failures don't stop the remaining pushes, so a release can still be created for what was uploaded.
/// </remarks>
internal sealed class PublishCommand(ActionsContext context, IProcessRunner processes)
{
    public const string NuGetOrg = "NuGet.org";
    public const string NuGetOrgSymbols = "NuGet.org symbols";

    private readonly PublishState state = new(context);

    public async Task RunAsync()
    {
        var manifest = state.Manifest;
        var feeds = ConfiguredFeeds();
        var nugetFeed = feeds.FirstOrDefault(feed => feed.Name == NuGetOrg);
        var results = new List<PublishResult>();
        var newlyUploaded = new List<PackageEntry>();
        var hasFailures = false;

        foreach (var package in manifest)
        {
            var statuses = new Dictionary<string, PushStatus>();

            foreach (var feed in feeds)
            {
                statuses[feed.Name] = await PushAsync(package.Path, feed, noSymbols: true);
            }

            var symbolsPath = Path.ChangeExtension(package.Path, ".snupkg");

            if (nugetFeed is not null && File.Exists(symbolsPath))
            {
                statuses[NuGetOrgSymbols] = await PushAsync(symbolsPath, nugetFeed with { Name = NuGetOrgSymbols }, noSymbols: false);
            }

            hasFailures |= statuses.Values.Any(status => status.IsProblem());
            results.Add(new PublishResult(package.Id, package.Version, statuses));

            if (statuses.Any(pair => pair.Key != NuGetOrgSymbols && pair.Value == PushStatus.Uploaded))
            {
                newlyUploaded.Add(package);
            }
        }

        state.Results = results;
        state.NewlyUploaded = newlyUploaded;
        context.SetOutput("has-new", newlyUploaded.Count > 0 ? "true" : "false");
        context.SetOutput("has-failures", hasFailures ? "true" : "false");
        context.AppendSummary(Summary(feeds, results, newlyUploaded.Count, includeSymbols: nugetFeed is not null));
    }

    internal IReadOnlyList<Feed> ConfiguredFeeds()
    {
        var feeds = new List<Feed>();

        if (context.GetBool("PUSH_TO_NUGET", true))
        {
            feeds.Add(new Feed(NuGetOrg, "https://api.nuget.org/v3/index.json", context.Get("NUGET_API_KEY")));
        }

        if (context.GetBool("PUSH_TO_GITHUB", true))
        {
            var owner = context.Require("GITHUB_REPOSITORY_OWNER");
            feeds.Add(new Feed("GitHub Packages", $"https://nuget.pkg.github.com/{owner}/index.json", context.Get("GITHUB_PACKAGES_TOKEN")));
        }

        if (context.GetBool("PUSH_TO_CUSTOM_FEEDS", false))
        {
            var urls = context.GetLines("CUSTOM_FEED_URLS");

            if (urls.Count == 0)
            {
                throw new WorkflowException("push-to-custom-feeds requires at least one custom-feed-url");
            }

            // Keys are matched to URLs by line number, so blank lines must not shift them.
            var keys = context.Get("CUSTOM_FEED_API_KEYS").ReplaceLineEndings("\n").Split('\n');
            feeds.AddRange(urls.Select((url, index) => new Feed($"Custom feed {index + 1}", ResolveSource(url), index < keys.Length ? keys[index].Trim() : "")));
        }

        return feeds;
    }

    /// <summary>
    /// NuGet only accepts absolute folder paths as sources, so a relative folder path is resolved
    /// from the repository root. URLs stay as they are.
    /// </summary>
    private string ResolveSource(string source)
    {
        return source.Contains("://") || Path.IsPathRooted(source) ? source : context.ResolvePath(source);
    }

    private async Task<PushStatus> PushAsync(string packagePath, Feed feed, bool noSymbols)
    {
        var name = Path.GetFileName(packagePath);

        if (feed.ApiKey.Length == 0)
        {
            context.Log.WriteLine($"{name} -> {feed.Name}: missing API key");
            return PushStatus.Failed;
        }

        List<string> arguments =
        [
            "nuget", "push", packagePath,
            "--api-key", feed.ApiKey, "--source", feed.Url,
            "--skip-duplicate", "--force-english-output",
        ];

        if (noSymbols)
        {
            arguments.Add("--no-symbols");
        }

        var result = await processes.RunAsync("dotnet", arguments, echo: false);
        var output = result.Output.Replace(feed.ApiKey, "[redacted]");
        var status = PushOutput.Classify(result.ExitCode, output);

        context.Log.WriteLine($"{name} -> {feed.Name}: {status.ToDisplayString()}");

        if (status.IsProblem())
        {
            context.Log.WriteLine(output);
        }

        return status;
    }

    private static string Summary(IReadOnlyList<Feed> feeds, IReadOnlyList<PublishResult> results, int newlyUploaded, bool includeSymbols)
    {
        List<string> headings = ["Package", .. feeds.Select(feed => feed.Name)];

        if (includeSymbols)
        {
            headings.Add("Symbols");
        }

        var summary = new StringBuilder();
        summary.Append("## NuGet publishing\n\n");
        summary.Append($"| {string.Join(" | ", headings)} |\n");
        summary.Append($"| {string.Join(" | ", headings.Select(_ => "---"))} |\n");

        foreach (var result in results)
        {
            List<string> values =
            [
                $"{result.Id} {result.Version}",
                .. feeds.Select(feed => result.Feeds.TryGetValue(feed.Name, out var status) ? status.ToDisplayString() : "not selected"),
            ];

            if (includeSymbols)
            {
                values.Add(result.Feeds.TryGetValue(NuGetOrgSymbols, out var status) ? status.ToDisplayString() : "none");
            }

            summary.Append($"| {string.Join(" | ", values)} |\n");
        }

        summary.Append($"\nNewly uploaded packages: {newlyUploaded}.\n");
        return summary.ToString();
    }
}
