using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantApi.HealthChecks;

/// <summary>
/// ASP.NET Core health check that verifies the PostgreSQL database is reachable.
/// Tagged as "ready" so it is included in the GET /health/ready readiness probe.
/// A failing Postgres check means conversation history, job status, and
/// prompt templates cannot be read or written.
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly IMetadataRepository _metadata;

    public PostgresHealthCheck(IMetadataRepository metadata) => _metadata = metadata;

    /// <summary>Executes SELECT 1 against PostgreSQL and reports healthy if it succeeds.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var healthy = await _metadata.IsHealthyAsync(ct);
        return healthy
            ? HealthCheckResult.Healthy("PostgreSQL is reachable")
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable");
    }
}
