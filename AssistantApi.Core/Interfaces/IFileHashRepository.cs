using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

public interface IFileHashRepository
{
    Task<FileHash?> GetAsync(Guid repositoryId, string filePath, CancellationToken ct = default);
    Task UpsertAsync(FileHash fileHash, CancellationToken ct = default);
    Task<IReadOnlyList<FileHash>> GetByRepositoryAsync(Guid repositoryId, CancellationToken ct = default);
    Task DeleteByRepositoryAsync(Guid repositoryId, CancellationToken ct = default);
}
