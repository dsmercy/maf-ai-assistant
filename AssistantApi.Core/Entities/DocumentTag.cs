namespace AssistantApi.Core.Entities;

/// <summary>
/// Stores the auto-detected categorisation metadata for an ingested instruction document.
/// One row per unique (language, category) pair discovered during ingestion.
/// Used by ITagVocabularyCache to build the live tag vocabulary at startup/cache-miss,
/// and by InstructionAgent to find which tags to filter on at query time.
/// </summary>
public class DocumentTag
{
    public int Id { get; set; }

    /// <summary>Normalised language tag (e.g. "csharp", "python", "typescript").</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Category within the language (e.g. "ef-core", "async-patterns").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Comma-separated keywords extracted from the document by the LLM.</summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>Short summary of the document produced by the LLM during ingestion.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// The Qdrant point ID this tag row corresponds to, allowing cache rebuilds
    /// to correlate DB rows back to vector points.
    /// </summary>
    public string PointId { get; set; } = string.Empty;

    /// <summary>Source file name of the ingested instruction document.</summary>
    public string SourceFile { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
