---
language: csharp
category: code-quality
priority: high
applies_to: code-generation, code-review
---
# C# Code Quality Standards

## Always Use
- **Records** for DTOs and value objects: `public sealed record OrderDto(Guid Id, string Number);`
- **File-scoped namespaces**: `namespace MyCompany.Features.Orders;`
- **Primary constructors**: `public sealed class OrderService(IOrderRepository repo) { }`
- **Nullable reference types** enabled everywhere — never `#nullable disable`
- **Guard clauses** at function entry:
  ```csharp
  ArgumentNullException.ThrowIfNull(customer);
  if (orderId == Guid.Empty) throw new ArgumentException("OrderId cannot be empty.");
  ```
- **Pattern matching** over large if/else chains
- **Collection expressions**: `string[] values = ["A", "B"];`
- **Required members**: `public required string Name { get; init; }`

## Always Avoid
| Anti-Pattern | Reason |
|---|---|
| `catch(Exception)` without handling | Silently swallows errors |
| TODO stubs left in production code | Incomplete implementation |
| Multiple enumeration of IEnumerable | Hidden performance cost |
| Public mutable state on aggregate roots | Broken invariants |
| Cyclic project dependencies | Architecture violations |
| Magic strings for configuration keys | Scattered, untestable config access |

## Static Analysis
`dotnet build -warnaserror` — zero warnings required in CI.
Enable: Microsoft analyzers, nullable reference types, security analyzers.
`dotnet format --verify-no-changes` in CI to enforce consistent style.

## Caching
Use only when measurable performance value exists. Prefer `IMemoryCache` / `IDistributedCache`.
Always define expiration policies. Always handle cache invalidation. Never cache sensitive data.

## Configuration — Options Pattern
```csharp
builder.Services
    .AddOptions<MyOptions>()
    .Bind(configuration.GetSection("MyOptions"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```
Validate configuration at startup. Avoid magic strings. Avoid scattered `IConfiguration` access.
