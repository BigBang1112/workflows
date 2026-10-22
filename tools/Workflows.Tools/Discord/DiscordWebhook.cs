using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Workflows.Tools.GitHub;

namespace Workflows.Tools.Discord;

/// <summary>
/// Posts messages to a Discord webhook, split into several messages when needed.
/// </summary>
internal sealed class DiscordWebhook(HttpClient http, Func<TimeSpan, Task> delay)
{
    /// <summary>Discord allows 2000 characters, which leaves room for formatting differences.</summary>
    public const int MessageLimit = 1900;

    private const int MaxAttempts = 4;

    public DiscordWebhook(HttpClient http) : this(http, time => Task.Delay(time))
    {
    }

    public async Task PostAsync(string webhookUrl, string message)
    {
        // Split everything first, so an overlong line can't cause a partial announcement.
        var chunks = Split(message).ToList();

        foreach (var chunk in chunks)
        {
            await PostChunkAsync(webhookUrl, chunk);
        }
    }

    /// <summary>
    /// Splits a message at line breaks into chunks of at most <see cref="MessageLimit"/> UTF-16 code units,
    /// which is how Discord counts characters. Lines are never split.
    /// </summary>
    public static IEnumerable<string> Split(string message, int limit = MessageLimit)
    {
        var chunk = "";

        foreach (var line in SplitKeepingLineBreaks(message))
        {
            if (line.Length > limit)
            {
                throw new WorkflowException("A Discord message line exceeds the limit and cannot be split at a line break");
            }

            if (chunk.Length + line.Length > limit)
            {
                yield return chunk;
                chunk = "";
            }

            chunk += line;
        }

        if (chunk.Length > 0)
        {
            yield return chunk;
        }
    }

    private static IEnumerable<string> SplitKeepingLineBreaks(string text)
    {
        var start = 0;

        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            end = end < 0 ? text.Length : end + 1;
            yield return text[start..end];
            start = end;
        }
    }

    private async Task PostChunkAsync(string webhookUrl, string chunk)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var response = await http.PostAsJsonAsync(webhookUrl, new { content = chunk });

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
            {
                await delay(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1));
                continue;
            }

            // The webhook URL is a secret, so the error only contains Discord's own message.
            var detail = await ReadErrorDetailAsync(response);
            var suffix = detail.Length > 0 ? $": {detail}" : "";
            throw new WorkflowException($"Discord webhook request failed with HTTP {(int)response.StatusCode}{suffix}");
        }
    }

    private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response)
    {
        var body = (await response.Content.ReadAsStringAsync()).Trim();

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
