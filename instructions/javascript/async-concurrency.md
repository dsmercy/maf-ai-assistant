---
language: javascript
category: async-concurrency
priority: high
applies_to: code-generation, code-review
---
# Node.js Async & Concurrency

## Promise Handling
Every promise must be awaited, returned, or explicitly handled. Never leave floating promises.

## Parallel Execution
For independent operations use `Promise.all` — never await sequentially when operations can run in parallel:
```javascript
// Good — parallel
const [user, orders] = await Promise.all([getUser(id), getOrders(id)]);

// Bad — sequential when independent
const user = await getUser(id);
const orders = await getOrders(id);
```

## Anti-patterns
| Anti-Pattern | Reason |
|---|---|
| `var` | Function-scoped bugs |
| `require()` | Legacy CommonJS module system |
| `eval()` / `new Function()` with user input | Security risk |
| Unhandled Promise rejections | Runtime instability, process crash |
| Mutating function arguments | Hidden side effects |
| Floating `void someAsyncWork()` | Failures are unobservable |

## Code Quality — Always Use
- `const` by default, `let` only when mutation required
- `async/await` over callback patterns
- Optional chaining: `user?.name ?? 'Unknown'`
- `structuredClone()` for deep copies
- Early returns to reduce nesting
- Small, focused, pure functions where practical

## Event Loop
Never block the event loop: no blocking I/O, no large synchronous operations, no reading entire large files into memory.
Prefer streams, pagination, and lazy processing for large data:
```javascript
pipeline(readStream, transformStream, writeStream);
```

## Modern Node.js Built-ins
Prefer platform capabilities before third-party packages:
`fetch()`, `AbortController`, Web Streams, `structuredClone()`, `node:test`
