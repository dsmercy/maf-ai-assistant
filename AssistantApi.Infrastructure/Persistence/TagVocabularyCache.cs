using AssistantApi.Application.Configuration;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Infrastructure.Persistence;

/// <summary>
/// Singleton cache of the tag vocabulary derived from DocumentTag rows.
/// On first call (or after TTL expiry), resolves a scoped IDocumentTagRepository
/// and IOllamaClient via IServiceProvider to rebuild the entry list including
/// pre-computed embeddings, then holds the result in memory.
/// TTL is 5 minutes — newly ingested documents are reflected within that window.
/// Invalidate() is called by IngestionPipeline immediately after a successful
/// instruction upload so the next query always sees fresh tags.
/// </summary>
public sealed class TagVocabularyCache : ITagVocabularyCache
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<TagVocabularyCache> _logger;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    private IReadOnlyList<TagVocabularyEntry> _cache = [];
    private DateTime _builtAt = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TagVocabularyCache(IServiceProvider sp, ILogger<TagVocabularyCache> logger)
    {
        _sp     = sp;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TagVocabularyEntry>> GetAsync(CancellationToken ct = default)
    {
        if (_cache.Count > 0 && DateTime.UtcNow - _builtAt < _ttl)
            return _cache;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cache.Count > 0 && DateTime.UtcNow - _builtAt < _ttl)
                return _cache;

            _cache    = await BuildAsync(ct);
            _builtAt  = DateTime.UtcNow;
            _logger.LogInformation("TagVocabularyCache rebuilt — {Count} entries", _cache.Count);
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _builtAt = DateTime.MinValue;
        _logger.LogDebug("TagVocabularyCache invalidated");
    }

    private async Task<IReadOnlyList<TagVocabularyEntry>> BuildAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var repo    = scope.ServiceProvider.GetRequiredService<IDocumentTagRepository>();
        var ollama  = scope.ServiceProvider.GetRequiredService<IOllamaClient>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AssistantOptions>>().Value;

        var tags = await repo.GetAllAsync(ct);
        if (tags.Count == 0)
        {
            _logger.LogWarning("TagVocabularyCache: no DocumentTag rows found — instruction uploads may not have run yet");
            return [];
        }

        // Deduplicate by language+category — take the most recent summary per pair
        var distinct = tags
            .GroupBy(t => $"{t.Language}|{t.Category}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(t => t.UpdatedAt).First())
            .ToList();

        // Build one embedding text per entry and batch-embed them
        var embeddingTexts = distinct
            .Select(t => $"{t.Language} {t.Category} {t.Keywords} {t.Summary}")
            .ToList();

        IReadOnlyList<float[]> embeddings;
        try
        {
            embeddings = await ollama.EmbedBatchAsync(options.EmbeddingModel, embeddingTexts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TagVocabularyCache: failed to embed tag vocabulary — returning empty cache");
            return [];
        }

        return distinct
            .Zip(embeddings, (tag, emb) => new TagVocabularyEntry(
                tag.Language,
                tag.Category,
                tag.Keywords,
                tag.Summary,
                emb))
            .ToList();
    }
}
