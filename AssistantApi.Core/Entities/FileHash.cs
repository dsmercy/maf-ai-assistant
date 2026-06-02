namespace AssistantApi.Core.Entities;

public class FileHash
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RepositoryId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}
