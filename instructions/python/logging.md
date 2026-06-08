---
language: python
category: python-logging
priority: medium
applies_to: code-generation
---
# Python Logging with structlog

## Structured Key-Value Format
Always use structured key=value format — never f-strings or string concatenation in log calls:
```python
# Good
logger.info("order_created", order_id=str(order.id), customer_id=str(customer.id))
logger.error("payment_failed", error=str(e), order_id=str(order.id), amount=order.total)

# Bad
logger.info(f"Order {order.id} created")
print(f"Error: {e}")
```

## Never Log
- Secrets, API keys, tokens
- Passwords or credentials
- PII (names, emails, SSNs, health data)
- Full file contents
- Connection strings

## Correlation IDs
Include correlation IDs on every request-scoped log entry.
Bind the request context at middleware level using `structlog.contextvars`:
```python
structlog.contextvars.bind_contextvars(request_id=request_id, correlation_id=correlation_id)
```

## Log Levels
- `debug` — internal diagnostic detail (not in production hot paths)
- `info` — notable business events (order created, job completed)
- `warning` — unexpected but recoverable (retry triggered, fallback used)
- `error` — failures that need investigation
Avoid noisy debug logs in hot paths — they add latency.
