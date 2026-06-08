---
language: csharp
category: cqrs-mediatr
priority: high
applies_to: code-generation, code-review
---
# C# CQRS & MediatR

## Commands
Commands mutate state, have side effects, return minimal results.
```csharp
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<OrderItem> Items) : IRequest<Guid>;
public sealed record CancelOrderCommand(Guid OrderId) : IRequest;
```

## Queries
Queries are read-only, have no side effects, optimized for reads.
```csharp
public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderDto>;
public sealed record GetOrdersQuery(int Page, int PageSize) : IRequest<PagedResult<OrderDto>>;
```

## Handlers
One responsibility per handler. Thin — orchestrate only, no business rule duplication.
Never call one handler from another handler.
```csharp
public sealed class CreateOrderHandler(IOrderRepository repository) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Items);
        await repository.AddAsync(order, ct);
        return order.Id;
    }
}
```

## Pipeline Behaviours
Use MediatR behaviours for cross-cutting concerns — never repeat them inside handlers:
- Validation (FluentValidation)
- Authorisation
- Structured logging
- Performance monitoring
- Transaction management
