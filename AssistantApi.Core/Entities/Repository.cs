namespace AssistantApi.Core.Entities;

public class Repository
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string Name { get; set; } = string.Empty;
    public IndexingStatus Status { get; set; } = IndexingStatus.Pending;
    public int FileCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastIndexedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum IndexingStatus
{
    Pending,
    Indexing,
    Completed,
    Failed
}
