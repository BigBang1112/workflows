using System.Net;
using System.Text.Json;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.Tests.Fakes;

/// <summary>
/// Answers GitHub API requests for one pull request from in-memory data, and records every write.
/// </summary>
internal sealed class FakeGitHub
{
    private const string Prefix = "/repos/owner/repo/";

    public FakeGitHub()
    {
        Handler = new FakeHttpHandler((request, _) => Respond(request));
        Api = new GitHubApi(new HttpClient(Handler), "https://api.github.test", "token");
    }

    public FakeHttpHandler Handler { get; }

    public GitHubApi Api { get; }

    public string Author { get; set; } = "contributor";

    public string State { get; set; } = "open";

    public List<object> Commits { get; } = [];

    public List<object> Comments { get; } = [];

    /// <summary>Writes as "METHOD path body", in order.</summary>
    public IEnumerable<string> Writes => Handler.Requests
        .Where(request => request.Request.Method != HttpMethod.Get)
        .Select(request => $"{request.Request.Method} {request.Request.RequestUri!.AbsolutePath} {request.Body}");

    public void AddCommit(string sha, string message, string authorName = "Jane Doe", string authorEmail = "jane@example.com", string? login = "jane")
    {
        Commits.Add(new
        {
            sha,
            commit = new { message, author = new { name = authorName, email = authorEmail } },
            author = login is null ? null : new { login },
        });
    }

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        var page = request.RequestUri.Query.Contains("page=1") || request.RequestUri.Query.Length == 0;

        if (request.Method == HttpMethod.Get)
        {
            return path switch
            {
                Prefix + "pulls/7" => Json(new { state = State, user = new { login = Author }, @base = new { @ref = "main" } }),
                Prefix + "pulls/7/commits" => Json(page ? Commits : []),
                Prefix + "issues/7/comments" => Json(page ? Comments : []),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"message":"Not Found"}""") },
            };
        }

        return Json(new { });
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value)),
    };
}
