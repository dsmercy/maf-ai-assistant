---
language: csharp
category: csharp-testing
priority: high
applies_to: unit-test
---
# C# Testing Standards

Frameworks: xUnit + Moq + FluentAssertions.
Validation: `dotnet test`

## Test Structure
Always use Arrange / Act / Assert sections with comments:
```csharp
[Fact]
public async Task Cancel_WhenOrderIsPending_ShouldSetStatusToCancelled()
{
    // Arrange
    var order = Order.Create(CustomerId, Items);

    // Act
    order.Cancel();

    // Assert
    order.Status.Should().Be(OrderStatus.Cancelled);
}
```

## Naming
`MethodName_Scenario_ExpectedBehaviour`
Examples: `Cancel_WhenAlreadyCancelled_ShouldThrow`, `GetById_WhenNotFound_ShouldReturnNull`

## Unit Tests
Test: business rules, domain services, application logic.
Never call: real HTTP endpoints, real databases, real file system in unit tests.
Mock all external boundaries with Moq.

## Integration Tests
Use `WebApplicationFactory<TProgram>` for API integration tests.
Do not mock infrastructure that is under test (e.g. do not mock EF Core when testing repositories).
Use for: full API request/response, authorisation, EF Core queries, infrastructure integrations.

## FluentAssertions
Prefer fluent assertions over raw `Assert`:
```csharp
result.Should().NotBeNull();
result.Status.Should().Be(OrderStatus.Confirmed);
result.Items.Should().HaveCount(2);
```
