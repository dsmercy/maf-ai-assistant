using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

/// <summary>Persistence contract for audit log entries.</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken ct = default);
}
