using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Infrastructure.Persistence;

public class MetadataRepository : IMetadataRepository
{
    private readonly AssistantDbContext _db;
    private readonly ILogger<MetadataRepository> _logger;

    public MetadataRepository(AssistantDbContext db, ILogger<MetadataRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<Repository?> GetRepositoryAsync(Guid id, CancellationToken ct = default)
        => _db.Repositories.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Repository>> GetAllRepositoriesAsync(CancellationToken ct = default)
        => await _db.Repositories.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<Repository> AddRepositoryAsync(Repository repository, CancellationToken ct = default)
    {
        _db.Repositories.Add(repository);
        await _db.SaveChangesAsync(ct);
        return repository;
    }

    public async Task UpdateRepositoryAsync(Repository repository, CancellationToken ct = default)
    {
        _db.Repositories.Update(repository);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRepositoryAsync(Guid id, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([id], ct);
        if (repo is not null)
        {
            _db.Repositories.Remove(repo);
            await _db.SaveChangesAsync(ct);
        }
    }

    public Task<IngestionJob?> GetJobAsync(Guid id, CancellationToken ct = default)
        => _db.IngestionJobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IngestionJob> AddJobAsync(IngestionJob job, CancellationToken ct = default)
    {
        _db.IngestionJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task UpdateJobAsync(IngestionJob job, CancellationToken ct = default)
    {
        _db.IngestionJobs.Update(job);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL health check failed");
            return false;
        }
    }
}
