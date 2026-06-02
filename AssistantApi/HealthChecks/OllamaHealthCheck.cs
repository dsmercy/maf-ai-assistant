using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantApi.HealthChecks;

public class OllamaHealthCheck : IHealthCheck
{
    private readonly IOllamaClient _ollama;

    public OllamaHealthCheck(IOllamaClient ollama) => _ollama = ollama;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var healthy = await _ollama.IsHealthyAsync(ct);
        return healthy
            ? HealthCheckResult.Healthy("Ollama is reachable")
            : HealthCheckResult.Unhealthy("Ollama is not reachable");
    }
}
