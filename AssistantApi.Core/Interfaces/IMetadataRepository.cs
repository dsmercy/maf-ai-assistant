using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

public interface IMetadataRepository
{
    Task<Repository?> GetRepositoryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Repository>> GetAllRepositoriesAsync(CancellationToken ct = default);
    Task<Repository> AddRepositoryAsync(Repository repository, CancellationToken ct = default);
    Task UpdateRepositoryAsync(Repository repository, CancellationToken ct = default);
    Task DeleteRepositoryAsync(Guid id, CancellationToken ct = default);

    Task<IngestionJob?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IngestionJob>> GetQueuedJobsAsync(CancellationToken ct = default);
    Task<IngestionJob> AddJobAsync(IngestionJob job, CancellationToken ct = default);
    Task UpdateJobAsync(IngestionJob job, CancellationToken ct = default);

    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
