---
language: csharp
category: async-patterns
priority: high
applies_to: code-generation, code-review
---
# C# Async Patterns

## CancellationToken
Every async method must accept `CancellationToken cancellationToken` unless technically impossible.
Pass it through to all downstream async calls — never discard it.

## Anti-patterns — never use
| Anti-Pattern | Reason |
|---|---|
| `.Result` / `.Wait()` | Causes deadlocks in ASP.NET Core |
| `async void` | Exceptions are unobservable and crash the process |
| `Task.Run(...)` for long-running background work | Use BackgroundService instead |
| Blocking I/O inside async methods | Starves the thread pool |

## Streaming large results
Use `IAsyncEnumerable<T>` for large result sets to avoid buffering everything in memory:
```csharp
public async IAsyncEnumerable<OrderDto> GetAllAsync(
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var order in repository.StreamAsync(ct))
        yield return Map(order);
}
```

## Bounded concurrency
Use `SemaphoreSlim` when fanning out work. Never create unbounded parallel workloads.
```csharp
var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
await Parallel.ForEachAsync(items, ct, async (item, token) =>
{
    await semaphore.WaitAsync(token);
    try { await ProcessAsync(item, token); }
    finally { semaphore.Release(); }
});
```

## Performance
Consider `Span<T>` / `Memory<T>` only after profiling demonstrates measurable value.
Avoid: premature optimisation, loading unnecessary data, repeated enumeration, quadratic algorithms.
