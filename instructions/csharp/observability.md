---
language: csharp
category: csharp-observability
priority: medium
applies_to: code-generation
---
# C# Logging & Observability

## Structured Logging with ILogger / Serilog
Always use message templates with named placeholders — never string interpolation:
```csharp
// Good
logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, order.CustomerId);

// Bad
logger.LogInformation($"Order {order.Id} created");
```
Never log: secrets, tokens, passwords, connection strings, PII.

## Correlation
Include in every request-scoped log: CorrelationId, RequestId, TraceId.
Use Serilog enrichers: `WithCorrelationId()`, `FromLogContext()`.

## OpenTelemetry
Prefer OpenTelemetry for metrics, traces, and distributed diagnostics.
Critical workflows must be traceable end-to-end across service boundaries.
Use `Activity` API for custom spans within a request.

## Serilog Configuration
Write structured JSON to stdout — compatible with ELK, Seq, Loki, Application Insights.
Set minimum levels per namespace to avoid noise:
```json
"Override": { "Microsoft": "Warning", "Microsoft.EntityFrameworkCore": "Warning" }
```
