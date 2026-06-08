---
language: csharp
category: aspnetcore-api
priority: high
applies_to: code-generation, code-review
---
# ASP.NET Core API Standards

## Controller Design
Controllers must be thin: validate → dispatch via MediatR → return result. No business logic.
```csharp
[HttpPost]
public async Task<ActionResult<Guid>> Create(
    [FromBody] CreateOrderRequest request, CancellationToken ct)
{
    var validation = await _validator.ValidateAsync(request, ct);
    if (!validation.IsValid)
        return BadRequest(validation.Errors);

    var id = await _mediator.Send(new CreateOrderCommand(request.CustomerId, request.Items), ct);
    return CreatedAtAction(nameof(GetById), new { id }, id);
}
```

## Authorisation
Secure by default — `[Authorize]` is the default policy.
Use `[AllowAnonymous]` explicitly and deliberately.
Prefer authorisation policies over hardcoded role strings.

## Validation
Use FluentValidation. Register pipeline behaviours for cross-cutting validation.
Validate all external input before it reaches business logic.

## API Design Rules
- Paginate all list endpoints — never return unbounded collections
- API versioning: `/api/v1/orders`
- OpenAPI/Swagger required for all public APIs — document requests, responses, errors, auth
- Never expose stack traces or internal error details to clients
- Return RFC 9457 ProblemDetails for all error responses

## Background Processing
Prefer `BackgroundService` / `IHostedService`. Support cancellation, log all failures, retry safely, handle shutdown gracefully.
Never use `Task.Run(...)` for long-running background jobs.
