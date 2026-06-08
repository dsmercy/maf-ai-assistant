---
language: general-observability
category: observability
priority: medium
applies_to: code-generation
---
# Observability Standards (Cross-Stack)

Always preserve existing logging, metrics, tracing, and monitoring when modifying code.

## Structured Logging Rules

Never use string interpolation or concatenation in log calls. Always use structured placeholders/key-value pairs.

| Stack | Good | Bad |
|---|---|---|
| C# | `logger.LogInformation("Order {OrderId} created", id)` | `logger.LogInformation($"Order {id} created")` |
| Node.js | `logger.info({ orderId }, 'Order created')` | `console.log(...)` |
| Python | `logger.info("order_created", order_id=str(id))` | `print(...)` or f-strings in log calls |
| React/TS | Use centralised logger | `console.log()` in production code |

## Always include in request-scoped logs

- CorrelationId
- RequestId  
- TraceId

## OpenTelemetry

Prefer OpenTelemetry for metrics, traces, and distributed diagnostics across all stacks.
Critical workflows must be traceable end-to-end.

## For every critical flow ensure

- Errors are logged with sufficient context to diagnose from logs alone
- Failures are diagnosable without accessing production systems
- Operational visibility remains intact after the change

## Deployability

Prefer solutions that: support safe rollback, minimise deployment risk, avoid downtime.
Highlight: irreversible operations, migration dependencies, deployment sequencing requirements.
