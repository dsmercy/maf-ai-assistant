using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantApi.HealthChecks;

public class PostgresHealthCheck : IHealthCheck
{
    private readonly IMetadataRepository _metadata;

    public PostgresHealthCheck(IMetadataRepository metadata) => _metadata = metadata;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var healthy = await _metadata.IsHealthyAsync(ct);
        return healthy
            ? HealthCheckResult.Healthy("PostgreSQL is reachable")
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable");
    }
}
