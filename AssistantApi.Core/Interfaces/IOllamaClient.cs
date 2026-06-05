namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Abstraction over the Ollama HTTP API.
/// Provides chat completion (blocking and streaming) and text embedding.
/// All implementations should apply retry and timeout policies appropriate
/// for large local LLM inference times.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Sends a chat completion request and waits for the full response.
    /// Suitable for non-streaming API calls where the caller needs the complete text.
    /// </summary>
    /// <param name="model">Ollama model name (e.g. "qwen3-coder:30b").</param>
    /// <param name="messages">Ordered list of messages forming the conversation context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's full response text.</returns>
    Task<string> ChatAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default);

    /// <summary>
    /// Sends a chat completion request and streams response tokens as they are generated.
    /// Used by the streaming endpoint to deliver tokens to the client progressively.
    /// </summary>
    /// <param name="model">Ollama model name.</param>
    /// <param name="messages">Ordered list of messages forming the conversation context.</param>
    /// <param name="ct">Cancellation token — cancel to stop the stream.</param>
    /// <returns>Async sequence of token strings.</returns>
    IAsyncEnumerable<string> ChatStreamAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default);

    /// <summary>
    /// Converts a single text string into a dense vector representation using the embedding model.
    /// Used by RepositoryAgent and InstructionAgent to embed user queries for Qdrant search.
    /// </summary>
    /// <param name="model">Embedding model name (e.g. "nomic-embed-text").</param>
    /// <param name="text">The text to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A float array of 768 dimensions (nomic-embed-text output size).</returns>
    Task<float[]> EmbedAsync(string model, string text, CancellationToken ct = default);

    /// <summary>
    /// Embeds multiple texts in a single Ollama API call using the /api/embed batch endpoint.
    /// Significantly faster than calling EmbedAsync in a loop during bulk ingestion.
    /// </summary>
    /// <param name="model">Embedding model name.</param>
    /// <param name="texts">List of texts to embed in one batch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of float arrays, one per input text, in the same order.</returns>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(string model, IReadOnlyList<string> texts, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the Ollama service is reachable and responding.
    /// Used by the OllamaHealthCheck for the /health/ready endpoint.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

/// <summary>A single message in a chat conversation, with a role and content.</summary>
/// <param name="Role">Either "system", "user", or "assistant".</param>
/// <param name="Content">The text content of the message.</param>
public record ChatMessage(string Role, string Content);
