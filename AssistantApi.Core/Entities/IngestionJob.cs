namespace AssistantApi.Core.Entities;

public class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? RepositoryId { get; set; }
    public IngestionJobType JobType { get; set; }
    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Queued;
    public string? SourcePath { get; set; }
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum IngestionJobType
{
    GitRepository,
    ZipUpload,
    Document,
    InstructionFile
}

public enum IngestionJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}
