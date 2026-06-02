using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Infrastructure.Ingestion;

public class EmbeddingPipeline
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly ILogger<EmbeddingPipeline> _logger;

    private const int BatchSize = 10;

    public EmbeddingPipeline(IOllamaClient ollama, IVectorRepository vectors, ILogger<EmbeddingPipeline> logger)
    {
        _ollama = ollama;
        _vectors = vectors;
        _logger = logger;
    }

    /// <summary>
    /// Embeds a list of (chunkId, text, metadata) tuples and upserts them into the given collection.
    /// </summary>
    public async Task EmbedAndUpsertAsync(
        string embeddingModel,
        string collection,
        IReadOnlyList<(string Id, string Text, Dictionary<string, string> Metadata)> items,
        CancellationToken ct = default)
    {
        var batches = items.Chunk(BatchSize);

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();

            var points = new List<VectorPoint>();
            foreach (var (id, text, metadata) in batch)
            {
                try
                {
                    var vector = await _ollama.EmbedAsync(embeddingModel, text, ct);
                    points.Add(new VectorPoint
                    {
                        Id = id,
                        Vector = vector,
                        Content = text,
                        Metadata = metadata
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to embed chunk {Id}", id);
                }
            }

            if (points.Count > 0)
                await _vectors.UpsertBatchAsync(collection, points, ct);

            _logger.LogDebug("Upserted batch of {Count} chunks to {Collection}", points.Count, collection);
        }
    }
}
