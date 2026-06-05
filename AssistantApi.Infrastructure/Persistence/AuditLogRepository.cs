using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

/// <summary>
/// Writes and reads audit log entries from PostgreSQL.
/// Uses a fire-and-forget insert pattern — audit failures must never break the request.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AssistantDbContext _db;

    public AuditLogRepository(AssistantDbContext db) => _db = db;

    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken ct = default)
        => await _db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
