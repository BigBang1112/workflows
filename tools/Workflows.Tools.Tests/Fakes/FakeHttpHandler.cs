using System.Net;

namespace Workflows.Tools.Tests.Fakes;

/// <summary>
/// Records every request and answers with a configured response instead of sending it.
/// </summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, int, HttpResponseMessage>? respond = null) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return respond?.Invoke(request, Requests.Count) ?? new HttpResponseMessage(HttpStatusCode.NoContent);
    }
}
