using AssistantApi.Core.Interfaces;

namespace AssistantApi.Infrastructure.Ingestion;

/// <summary>
/// Splits large text content into overlapping chunks suitable for embedding.
/// Uses a character-based approximation of token count (1 token ≈ 4 characters)
/// and splits at whitespace boundaries to avoid breaking words mid-token.
///
/// Overlapping chunks ensure that context spanning a chunk boundary is not lost —
/// each chunk shares <paramref name="overlap"/> tokens with the next chunk.
/// </summary>
public class ChunkingService : IChunkingService
{
    /// <summary>
    /// Splits the input text into overlapping chunks.
    /// </summary>
    /// <param name="text">The source text to split.</param>
    /// <param name="chunkSize">Target chunk size in tokens (approximated as characters / 4).</param>
    /// <param name="overlap">Number of tokens to overlap between consecutive chunks.</param>
    /// <returns>List of text chunks in order.</returns>
    public IReadOnlyList<string> Chunk(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var charChunk = chunkSize * 4;
        var charOverlap = overlap * 4;

        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + charChunk, text.Length);

            // Walk back to the nearest whitespace to avoid splitting mid-word
            if (end < text.Length)
            {
                var boundary = text.LastIndexOf(' ', end, Math.Min(end - start, 100));
                if (boundary > start) end = boundary;
            }

            var chunk = text[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            start = end - charOverlap;
            if (start <= 0 || start >= text.Length - charOverlap) break;
        }

        return chunks;
    }
}
