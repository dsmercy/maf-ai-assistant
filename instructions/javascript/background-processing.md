---
language: javascript
category: background-processing
priority: medium
applies_to: code-generation
---
# Node.js Background Processing

## Preferred Tools
- BullMQ for queue-based background jobs
- Worker Threads for CPU-intensive tasks
- Dedicated worker processes for isolated workloads

## Rules
Never use `void someAsyncWork()` for important background work — failures become unobservable.
Never fire-and-forget critical operations without error handling.

Background jobs must:
- Log all failures with enough context to diagnose
- Support retries with bounded attempts
- Support monitoring and visibility (job status, queue depth)
- Be idempotent where possible — safe to run twice

## Graceful Shutdown
Always handle SIGINT and SIGTERM:
```javascript
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);

async function shutdown() {
    await server.close();       // 1. Stop accepting new requests
    await queue.close();        // 2. Finish in-flight jobs
    await db.end();             // 3. Close DB connections
    await logger.flush();       // 4. Flush buffered logs
    process.exit(0);
}
```
Shutdown sequence: stop accepting → finish active → close DB → close queues → flush logs.
