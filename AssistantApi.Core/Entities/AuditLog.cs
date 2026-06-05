namespace AssistantApi.Core.Entities;

/// <summary>
/// Immutable record of every API call made to the assistant.
/// Written by AuditMiddleware on every request/response cycle.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Distributed trace ID for correlating logs across services.</summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>Identity of the caller — email/sub from JWT or "anonymous".</summary>
    public string UserId { get; set; } = "anonymous";

    /// <summary>HTTP method (GET, POST, DELETE, etc.).</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Request path without query string.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>HTTP response status code.</summary>
    public int StatusCode { get; set; }

    /// <summary>Total request duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent header value.</summary>
    public string? UserAgent { get; set; }

    /// <summary>UTC timestamp when the request was received.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
