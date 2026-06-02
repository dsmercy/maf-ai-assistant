using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantApi.HealthChecks;

public class QdrantHealthCheck : IHealthCheck
{
    private readonly IVectorRepository _vectors;

    public QdrantHealthCheck(IVectorRepository vectors) => _vectors = vectors;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var healthy = await _vectors.IsHealthyAsync(ct);
        return healthy
            ? HealthCheckResult.Healthy("Qdrant is reachable")
            : HealthCheckResult.Unhealthy("Qdrant is not reachable");
    }
}
