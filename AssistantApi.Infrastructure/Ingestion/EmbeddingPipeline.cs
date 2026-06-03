using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Infrastructure.Ingestion;

public class EmbeddingPipeline
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly ILogger<EmbeddingPipeline> _logger;

    // How many chunks to send to Ollama in one /api/embed call
    private const int EmbedBatchSize = 32;

    public EmbeddingPipeline(IOllamaClient ollama, IVectorRepository vectors, ILogger<EmbeddingPipeline> logger)
    {
        _ollama = ollama;
        _vectors = vectors;
        _logger = logger;
    }

    /// <summary>
    /// Embeds all items using Ollama batch embed, then upserts to Qdrant.
    /// One /api/embed call per EmbedBatchSize chunks instead of one call per chunk.
    /// </summary>
    public async Task EmbedAndUpsertAsync(
        string embeddingModel,
        string collection,
        IReadOnlyList<(string Id, string Text, Dictionary<string, string> Metadata)> items,
        CancellationToken ct = default)
    {
        if (items.Count == 0) return;

        var batches = items.Chunk(EmbedBatchSize);

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();

            var batchList = batch.ToList();
            var texts = batchList.Select(b => b.Text).ToList();

            IReadOnlyList<float[]> vectors;
            try
            {
                vectors = await _ollama.EmbedBatchAsync(embeddingModel, texts, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch embed failed for {Count} chunks, skipping batch", batchList.Count);
                continue;
            }

            var points = batchList
                .Zip(vectors, (item, vector) => new VectorPoint
                {
                    Id = item.Id,
                    Vector = vector,
                    Content = item.Text,
                    Metadata = item.Metadata
                })
                .ToList();

            await _vectors.UpsertBatchAsync(collection, points, ct);
            _logger.LogDebug("Embedded and upserted {Count} chunks to {Collection}", points.Count, collection);
        }
    }
}
