using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

/// <summary>
/// Reads and writes feature flags from PostgreSQL.
/// Flags control optional behaviour (streaming, RAG, auth) that can be toggled
/// at runtime without redeploying the application.
/// </summary>
public class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly AssistantDbContext _db;

    public FeatureFlagRepository(AssistantDbContext db) => _db = db;

    public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
    {
        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(f => f.Name == name, ct);
        return flag?.IsEnabled ?? false;
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default)
        => await _db.FeatureFlags.OrderBy(f => f.Name).ToListAsync(ct);

    public async Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        var existing = await _db.FeatureFlags.FirstOrDefaultAsync(f => f.Name == flag.Name, ct);
        if (existing is null)
            _db.FeatureFlags.Add(flag);
        else
        {
            existing.IsEnabled = flag.IsEnabled;
            existing.Description = flag.Description;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
