using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Infrastructure.Ingestion;

/// <summary>
/// Converts text chunks into vector embeddings and upserts them into a Qdrant collection.
/// Uses the Ollama /api/embed batch endpoint to embed multiple chunks per HTTP call,
/// significantly reducing ingestion time compared to one call per chunk.
///
/// Batch size is controlled by <see cref="EmbedBatchSize"/>. Failed batches are
/// logged and skipped — partial indexing is better than aborting the entire job.
/// </summary>
public class EmbeddingPipeline
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly ILogger<EmbeddingPipeline> _logger;

    /// <summary>Number of chunks sent to Ollama in a single /api/embed call.</summary>
    private const int EmbedBatchSize = 32;

    public EmbeddingPipeline(IOllamaClient ollama, IVectorRepository vectors, ILogger<EmbeddingPipeline> logger)
    {
        _ollama = ollama;
        _vectors = vectors;
        _logger = logger;
    }

    /// <summary>
    /// Embeds all items in batches and upserts the resulting vectors into Qdrant.
    /// Each item is a tuple of (pointId, textContent, metadataDictionary).
    /// </summary>
    /// <param name="embeddingModel">Ollama model name to use for embedding (e.g. "nomic-embed-text").</param>
    /// <param name="collection">Target Qdrant collection name (e.g. "code-embeddings").</param>
    /// <param name="items">List of (Id, Text, Metadata) tuples to embed and store.</param>
    /// <param name="ct">Cancellation token.</param>
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
