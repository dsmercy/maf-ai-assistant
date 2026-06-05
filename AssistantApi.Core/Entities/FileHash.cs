namespace AssistantApi.Core.Entities;

/// <summary>
/// Stores a SHA-256 hash of each indexed file to enable incremental re-indexing.
/// When a repository is re-indexed, files whose hash has not changed are skipped,
/// avoiding redundant embedding calls and Qdrant upserts.
/// </summary>
public class FileHash
{
    /// <summary>Unique identifier for this hash record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The repository that owns this file.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Relative path of the file within the repository (e.g. src/Core/Interfaces/IUserRepository.cs).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>SHA-256 hex hash of the file contents at the time of last indexing.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Number of vector chunks generated from this file during last indexing.</summary>
    public int ChunkCount { get; set; }

    /// <summary>UTC timestamp of the last successful indexing of this file.</summary>
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}
