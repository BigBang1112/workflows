using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workflows.Tools.GitHub;

/// <summary>
/// A minimal client for the GitHub REST API.
/// </summary>
internal sealed class GitHubApi(HttpClient http, string apiUrl, string token)
{
    private const int PageSize = 100;

    public static GitHubApi FromContext(ActionsContext context, HttpClient http)
    {
        var apiUrl = context.Get("GITHUB_API_URL") is { Length: > 0 } url ? url : "https://api.github.com";
        return new GitHubApi(http, apiUrl, context.Get("GITHUB_TOKEN"));
    }

    public Task<JsonElement> GetAsync(string path) => SendAsync(HttpMethod.Get, path, null);

    /// <summary>Gets every page of a list endpoint.</summary>
    public async Task<IReadOnlyList<JsonElement>> GetAllPagesAsync(string path)
    {
        var items = new List<JsonElement>();

        for (var page = 1; ; page++)
        {
            var separator = path.Contains('?') ? '&' : '?';
            var pageItems = (await GetAsync($"{path}{separator}per_page={PageSize}&page={page}")).EnumerateArray().ToList();
            items.AddRange(pageItems);

            if (pageItems.Count < PageSize)
            {
                return items;
            }
        }
    }

    public Task<JsonElement> PostAsync(string path, object body) => SendAsync(HttpMethod.Post, path, body);

    public Task<JsonElement> PatchAsync(string path, object body) => SendAsync(HttpMethod.Patch, path, body);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, $"{apiUrl.TrimEnd('/')}/{path}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("bigbang1112-workflows", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        if (token.Length > 0)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new WorkflowException($"GitHub API request {method} {path} failed with HTTP {(int)response.StatusCode}: {ErrorMessage(text)}");
        }

        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private static string ErrorMessage(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? body : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
