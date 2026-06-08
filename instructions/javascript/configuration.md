---
language: javascript
category: nodejs-configuration
priority: medium
applies_to: code-generation
---
# Node.js Configuration & Logging

## Configuration
Use environment variables. Validate all required vars at startup with Zod — fail fast if missing:
```javascript
const ConfigSchema = z.object({
    DATABASE_URL: z.string().url(),
    PORT: z.coerce.number().default(3000),
    NODE_ENV: z.enum(['development', 'production', 'test']),
});

export const config = ConfigSchema.parse(process.env);
```
Never scatter `process.env` access across the codebase. Use a single typed config module.
Never hardcode configuration values.

## Structured Logging with Pino
Use Pino (or Winston) for all production logging. Never use `console.log()` or `console.error()`.
Always use structured key-value format — never string concatenation:
```javascript
// Good
logger.info({ orderId, customerId }, 'Order created');
logger.error({ err, orderId }, 'Order processing failed');

// Bad
logger.info(`Order ${orderId} created`);
console.log('Order created');
```
Never log: secrets, tokens, passwords, PII, connection strings.

## Correlation IDs
Include CorrelationId, RequestId, and TraceId in every request-scoped log entry.
Generate a correlation ID in middleware and attach to the request context.
