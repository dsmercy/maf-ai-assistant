using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;

namespace AssistantApi.Middleware;

/// <summary>
/// Records every API request in the audit_logs table.
/// Runs after authentication so the user identity is available.
/// Audit failures are swallowed — a logging error must never break a real request.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    // Paths that generate too much noise and don't need auditing
    private static readonly HashSet<string> SkippedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health", "/health/ready", "/swagger", "/favicon.ico"
    };

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await _next(context);

        sw.Stop();

        var path = context.Request.Path.Value ?? "/";
        if (SkippedPaths.Any(s => path.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            return;

        try
        {
            var userId = context.Items["UserId"] as string
                         ?? context.User.FindFirst("email")?.Value
                         ?? context.User.FindFirst("sub")?.Value
                         ?? "anonymous";

            var entry = new AuditLog
            {
                TraceId = context.TraceIdentifier,
                UserId = userId,
                Method = context.Request.Method,
                Path = path,
                StatusCode = context.Response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
                CreatedAt = start
            };

            // Fire-and-forget — use a fresh scope so the scoped DbContext is independent
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = context.RequestServices
                        .GetRequiredService<IServiceScopeFactory>()
                        .CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                    await repo.AddAsync(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write audit log entry");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditMiddleware encountered an error");
        }
    }
}
