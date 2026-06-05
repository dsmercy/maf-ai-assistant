using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantApi.HealthChecks;

/// <summary>
/// ASP.NET Core health check that verifies the Qdrant vector database is reachable.
/// Tagged as "ready" so it is included in the GET /health/ready readiness probe.
/// A failing Qdrant check means semantic search and vector upserts will fail.
/// </summary>
public class QdrantHealthCheck : IHealthCheck
{
    private readonly IVectorRepository _vectors;

    public QdrantHealthCheck(IVectorRepository vectors) => _vectors = vectors;

    /// <summary>Calls Qdrant health gRPC endpoint and reports healthy if it responds.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var healthy = await _vectors.IsHealthyAsync(ct);
        return healthy
            ? HealthCheckResult.Healthy("Qdrant is reachable")
            : HealthCheckResult.Unhealthy("Qdrant is not reachable");
    }
}
