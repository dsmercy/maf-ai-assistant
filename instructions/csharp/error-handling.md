---
language: csharp
category: error-handling
priority: high
applies_to: code-generation, code-review
---
# C# Error Handling

## Typed Exceptions
Use typed, descriptive exceptions. Never throw generic `Exception`.
```csharp
public sealed class OrderNotFoundException(Guid orderId)
    : Exception($"Order '{orderId}' not found.");

public sealed class OrderAlreadyCancelledException(Guid orderId)
    : Exception($"Order '{orderId}' is already cancelled.");
```

## Result Pattern
Use when failure is a normal expected outcome (not exceptional):
```csharp
public sealed record Result<T>(T? Value, string? Error, bool IsSuccess);
```

## Logging
Log then rethrow — never swallow exceptions silently:
```csharp
catch (OrderException ex)
{
    logger.LogWarning(ex, "Order {OrderId} operation failed", orderId);
    throw;
}
```
Never catch `Exception` without handling — it hides real errors.

## API Error Responses
Return RFC 9457 ProblemDetails. Never return plain "Something went wrong".
```csharp
return Problem(
    title: "Order not found",
    detail: $"Order {orderId} does not exist.",
    statusCode: StatusCodes.Status404NotFound);
```

## Polly Retry
Retries must be bounded, targeted to transient failures, and observable.
Never create infinite retry loops.
```csharp
pipeline.AddRetry(new RetryStrategyOptions
{
    MaxRetryAttempts = 3,
    Delay = TimeSpan.FromSeconds(2),
    BackoffType = DelayBackoffType.Exponential
});
```
