namespace AssistantApi.Core.Entities;

/// <summary>
/// Represents a source code repository registered for indexing.
/// Tracks the URL, branch, indexing status, and metadata about the indexed content.
/// </summary>
public class Repository
{
    /// <summary>Unique identifier for the repository record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The remote URL of the repository (HTTPS or SSH).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The branch to clone and index. Defaults to "main".</summary>
    public string Branch { get; set; } = "main";

    /// <summary>Derived short name of the repository (last segment of the URL without .git).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Current indexing status of the repository.</summary>
    public IndexingStatus Status { get; set; } = IndexingStatus.Pending;

    /// <summary>Number of files processed during the last successful indexing run.</summary>
    public int FileCount { get; set; }

    /// <summary>Total number of vector chunks stored in Qdrant for this repository.</summary>
    public int ChunkCount { get; set; }

    /// <summary>UTC timestamp when this repository record was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last successful indexing run. Null if never indexed.</summary>
    public DateTime? LastIndexedAt { get; set; }

    /// <summary>Error message from the most recent failed indexing attempt. Null if no error.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Local filesystem path where the repository was cloned inside the container.</summary>
    public string? LocalPath { get; set; }

    /// <summary>Optional personal access token for cloning private repositories.</summary>
    public string? Pat { get; set; }
}

/// <summary>
/// Represents the current state of a repository's indexing lifecycle.
/// </summary>
public enum IndexingStatus
{
    /// <summary>Registered but not yet queued for indexing.</summary>
    Pending,
    /// <summary>Currently being cloned and embedded.</summary>
    Indexing,
    /// <summary>Successfully indexed and searchable.</summary>
    Completed,
    /// <summary>Indexing failed. See Repository.ErrorMessage for details.</summary>
    Failed
}
