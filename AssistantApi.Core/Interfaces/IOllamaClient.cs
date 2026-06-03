namespace AssistantApi.Core.Interfaces;

public interface IOllamaClient
{
    Task<string> ChatAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default);
    IAsyncEnumerable<string> ChatStreamAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default);
    Task<float[]> EmbedAsync(string model, string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(string model, IReadOnlyList<string> texts, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public record ChatMessage(string Role, string Content);
