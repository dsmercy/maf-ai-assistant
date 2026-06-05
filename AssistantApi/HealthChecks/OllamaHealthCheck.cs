using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantApi.HealthChecks;

/// <summary>
/// ASP.NET Core health check that verifies the Ollama service is reachable.
/// Tagged as "ready" so it is included in the GET /health/ready readiness probe.
/// A failing Ollama check means chat and embedding requests will fail.
/// </summary>
public class OllamaHealthCheck : IHealthCheck
{
    private readonly IOllamaClient _ollama;

    public OllamaHealthCheck(IOllamaClient ollama) => _ollama = ollama;

    /// <summary>Calls Ollama /api/tags and reports healthy if the response is 200 OK.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var healthy = await _ollama.IsHealthyAsync(ct);
        return healthy
            ? HealthCheckResult.Healthy("Ollama is reachable")
            : HealthCheckResult.Unhealthy("Ollama is not reachable");
    }
}
