namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Abstraction over the Qdrant vector database.
/// Provides upsert, semantic search, and deletion operations on named collections.
/// The three collections used by this application are:
/// - code-embeddings: source code chunks from indexed repositories
/// - doc-embeddings: chunks from uploaded PDF, DOCX, MD, TXT documents
/// - instruction-embeddings: coding standards and rule files
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// Inserts or updates a single vector point in the specified collection.
    /// </summary>
    Task UpsertAsync(string collection, VectorPoint point, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates a batch of vector points in one Qdrant call.
    /// More efficient than calling UpsertAsync in a loop for bulk ingestion.
    /// </summary>
    Task UpsertBatchAsync(string collection, IEnumerable<VectorPoint> points, CancellationToken ct = default);

    /// <summary>
    /// Performs a cosine similarity search and returns the top-K most similar results.
    /// Optionally filters by metadata fields (e.g. repository name, language).
    /// </summary>
    /// <param name="collection">The Qdrant collection to search.</param>
    /// <param name="vector">Query vector produced by the embedding model.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <param name="filters">Optional key-value metadata filters applied before scoring.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collection, float[] vector, int topK = 5, Dictionary<string, string>? filters = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes all vector points whose metadata matches all specified filter conditions.
    /// Used to remove stale vectors when a file is deleted or a repository is de-registered.
    /// </summary>
    Task DeleteByFilterAsync(string collection, Dictionary<string, string> filters, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the Qdrant service is reachable and responding.
    /// Used by QdrantHealthCheck for the /health/ready endpoint.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

/// <summary>
/// A vector point to be stored in Qdrant. Contains the embedding vector,
/// the original text content, and metadata for filtering and display.
/// </summary>
public class VectorPoint
{
    /// <summary>Unique identifier for this point. Must be a valid UUID string.</summary>
    public required string Id { get; init; }

    /// <summary>The embedding vector produced by the embedding model.</summary>
    public required float[] Vector { get; init; }

    /// <summary>Key-value metadata stored alongside the vector for filtering and retrieval.</summary>
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>The original text content this vector represents. Stored as _content in metadata.</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// A single result from a Qdrant similarity search, including the similarity score
/// and the metadata and content stored with the vector.
/// </summary>
public class VectorSearchResult
{
    /// <summary>The UUID of the matching vector point in Qdrant.</summary>
    public required string Id { get; init; }

    /// <summary>Cosine similarity score between the query vector and this point (0.0 to 1.0).</summary>
    public double Score { get; init; }

    /// <summary>Metadata fields stored with this point (e.g. repository, file_path, language).</summary>
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>The original text content retrieved from the _content metadata field.</summary>
    public string Content { get; init; } = string.Empty;
}
