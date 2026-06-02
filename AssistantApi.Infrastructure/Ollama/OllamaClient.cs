using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Infrastructure.Ollama;

public class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
}

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient http, ILogger<OllamaClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> ChatAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false
        };

        var response = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonElement json;
            try { json = JsonSerializer.Deserialize<JsonElement>(line); }
            catch { continue; }

            if (json.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                var token = content.GetString();
                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }

            if (json.TryGetProperty("done", out var done) && done.GetBoolean())
                yield break;
        }
    }

    public async Task<float[]> EmbedAsync(string model, string text, CancellationToken ct = default)
    {
        var payload = new { model, prompt = text };
        var response = await _http.PostAsJsonAsync("/api/embeddings", payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("embedding")
            .EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama health check failed");
            return false;
        }
    }
}
