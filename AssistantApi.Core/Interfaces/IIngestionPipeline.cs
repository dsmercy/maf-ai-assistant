namespace AssistantApi.Core.Interfaces;

public interface IIngestionPipeline
{
    Task IngestRepositoryAsync(Guid jobId, Guid repositoryId, string localPath, string repoName, string branch, CancellationToken ct = default);
    Task IngestDocumentAsync(Guid jobId, string filePath, string fileName, DocumentCollection collection, CancellationToken ct = default);
}

public enum DocumentCollection
{
    Code,
    Documents,
    Instructions
}
