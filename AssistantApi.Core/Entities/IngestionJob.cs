namespace AssistantApi.Core.Entities;

/// <summary>
/// Represents a background ingestion job that processes a repository or document
/// into vector embeddings stored in Qdrant. Jobs are queued by the API and
/// processed asynchronously by the IngestionService worker.
/// </summary>
public class IngestionJob
{
    /// <summary>Unique identifier for this job.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The repository this job is indexing. Null for document/instruction jobs.</summary>
    public Guid? RepositoryId { get; set; }

    /// <summary>The type of source being ingested (Git repo, ZIP upload, document, or instruction file).</summary>
    public IngestionJobType JobType { get; set; }

    /// <summary>Current execution status of the job.</summary>
    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Queued;

    /// <summary>Filesystem path to the source file for upload-type jobs. Null for Git repository jobs.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Original filename as provided by the uploader. Used for deduplication — SourcePath uses a GUID temp name.</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>Number of files successfully parsed and embedded so far.</summary>
    public int ProcessedFiles { get; set; }

    /// <summary>Total number of files discovered for this job. Set when the job starts.</summary>
    public int TotalFiles { get; set; }

    /// <summary>UTC timestamp when the job was created and queued.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the worker picked up and started executing the job.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>UTC timestamp when the job finished (successfully or with failure).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Error message if the job failed. Null on success.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Specifies what type of content the ingestion job is processing.</summary>
public enum IngestionJobType
{
    /// <summary>Clone and index a Git repository via HTTPS.</summary>
    GitRepository,
    /// <summary>Extract and index a ZIP archive uploaded by the user.</summary>
    ZipUpload,
    /// <summary>Index a PDF, DOCX, MD, or TXT document into doc-embeddings.</summary>
    Document,
    /// <summary>Index a coding standards or rules file into instruction-embeddings.</summary>
    InstructionFile
}

/// <summary>Tracks the lifecycle state of an ingestion job.</summary>
public enum IngestionJobStatus
{
    /// <summary>Job is waiting in the queue to be picked up by the worker.</summary>
    Queued,
    /// <summary>Worker is actively processing this job.</summary>
    Running,
    /// <summary>Job finished successfully.</summary>
    Completed,
    /// <summary>Job encountered an unrecoverable error. See ErrorMessage for details.</summary>
    Failed
}
