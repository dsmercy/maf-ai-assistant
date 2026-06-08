---
language: csharp
category: ef-core
priority: high
applies_to: code-generation, code-review
---
# C# EF Core Standards

## Read Operations
Always use `AsNoTracking()` for read-only queries. Project to DTOs — never return tracked entities.
```csharp
var orders = await context.Orders
    .AsNoTracking()
    .Where(o => o.CustomerId == customerId)
    .Select(o => new OrderDto(o.Id, o.Status, o.CreatedAt))
    .ToListAsync(ct);
```

## Query Efficiency
Project only required columns with `.Select(...)`. Avoid loading full entities for read operations.
Prevent N+1: use `Include`/`ThenInclude` appropriately. Review generated SQL.
Use split queries when joins cause cartesian explosions:
```csharp
.AsSplitQuery()
```

## Concurrency
Use optimistic concurrency. Handle `DbUpdateConcurrencyException` explicitly — never silently overwrite.
```csharp
[Timestamp]
public byte[] RowVersion { get; set; } = default!;
```

## Migrations
Review every migration manually before applying. Never apply to production blindly.
Never allow accidental client-side query evaluation.

## Repository Guidance
Avoid generic `IRepository<T>`. Prefer DbContext directly in handlers, or feature-specific repositories when the abstraction adds meaningful value.
Only introduce a repository when it will be used in 3+ places or genuinely simplifies testing.

## Transactions
Commands own transaction boundaries. Queries must remain read-only.
Avoid long-running transactions and nested transactions. Keep transactions as short as possible.
