using AssistantApi.Core.Interfaces;

namespace AssistantApi.Infrastructure.Ingestion;

public class ChunkingService : IChunkingService
{
    public IReadOnlyList<string> Chunk(string text, int chunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        // Split on whitespace-bounded word boundaries to avoid breaking tokens mid-word.
        // Approximation: 1 token ≈ 4 characters (good enough for chunking purposes).
        var charChunk = chunkSize * 4;
        var charOverlap = overlap * 4;

        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + charChunk, text.Length);

            // Walk back to the nearest whitespace so we don't split mid-word
            if (end < text.Length)
            {
                var boundary = text.LastIndexOf(' ', end, Math.Min(end - start, 100));
                if (boundary > start) end = boundary;
            }

            var chunk = text[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            // Advance with overlap
            start = end - charOverlap;
            if (start <= 0 || start >= text.Length - charOverlap) break;
        }

        return chunks;
    }
}
