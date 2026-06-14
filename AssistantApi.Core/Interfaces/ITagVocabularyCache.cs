namespace AssistantApi.Core.Interfaces;

/// <summary>
/// In-memory cache of the tag vocabulary built from DocumentTag rows.
/// Registered as a singleton — populated on first access and refreshed every 5 minutes
/// so newly ingested instruction documents are picked up without a restart.
/// </summary>
public interface ITagVocabularyCache
{
    /// <summary>
    /// Returns all known tag entries. Rebuilds from DB if cache is empty or stale.
    /// </summary>
    Task<IReadOnlyList<TagVocabularyEntry>> GetAsync(CancellationToken ct = default);

    /// <summary>Immediately invalidates the cache, forcing a rebuild on next GetAsync call.</summary>
    void Invalidate();
}

/// <summary>
/// A single entry in the tag vocabulary — represents one language/category pair
/// with its associated summary text used for embedding similarity matching.
/// </summary>
public record TagVocabularyEntry(
    string Language,
    string Category,
    string Keywords,
    string Summary,
    /// <summary>Pre-computed embedding of "{language} {category} {keywords} {summary}" for similarity search.</summary>
    float[] Embedding);
