namespace AssistantApi.Application.DTOs;

public class JobStatusResponse
{
    public Guid Id { get; set; }
    public Guid? RepositoryId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public int ProgressPercent => TotalFiles > 0 ? (int)((double)ProcessedFiles / TotalFiles * 100) : 0;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => StartedAt.HasValue
        ? (CompletedAt ?? DateTime.UtcNow) - StartedAt.Value
        : null;
    public string? ErrorMessage { get; set; }
    public string? SourcePath { get; set; }
}

public class JobSummaryResponse
{
    public int Total { get; set; }
    public int Queued { get; set; }
    public int Running { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public List<JobStatusResponse> Jobs { get; set; } = [];
}
