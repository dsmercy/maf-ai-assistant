---
language: csharp
category: domain-modelling
priority: high
applies_to: code-generation, code-review
---
# C# Domain Modelling

## Entities
Entities own business rules and protect invariants. Avoid public setters on aggregate roots.
```csharp
// Good — behaviour on the entity
order.Cancel();
order.AddItem(productId, quantity);

// Bad — direct state mutation from outside
order.Status = OrderStatus.Cancelled;
order.Items.Add(item);
```

## Value Objects
Prefer immutable records. Value objects compare by value, never by reference.
```csharp
public sealed record EmailAddress(string Value);
public sealed record Money(decimal Amount, string Currency);
```

## Domain Events
Use for cross-aggregate communication, side effects, and integration triggers.
Avoid direct coupling between aggregates. Raise events inside entity methods.

## Domain Services
Use only when logic does not naturally belong to any single entity.
Avoid anemic domain models — behaviour belongs on entities, not external service classes.

## Dependency Injection in Domain
Constructor injection exclusively. Prefer primary constructors:
```csharp
public sealed class OrderService(IOrderRepository repository, ILogger<OrderService> logger) { }
```
Never use: Service Locator, static service access, IServiceProvider in domain/business code.
Lifetimes: Singleton = stateless only; Scoped = DbContext + request-scoped; Transient = lightweight stateless.
