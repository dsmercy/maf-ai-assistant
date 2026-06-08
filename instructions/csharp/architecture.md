---
language: csharp
category: architecture
priority: high
applies_to: code-generation, code-review
---
# C# Clean Architecture

Dependency direction — inner layers never reference outer layers:
```
Domain → Application → Infrastructure → API
```

Domain: entities, value objects, domain events, domain services, business rules.
Must NOT reference: EF Core, ASP.NET Core, infrastructure, or any external library.

Application: commands, queries, handlers, DTOs, validators, interfaces.
Orchestration only — no infrastructure concerns, no EF Core, no HTTP types.

Infrastructure: EF Core, external API clients, email, file storage, cache implementations.
Must implement abstractions defined in Application. Never referenced by Domain.

API: controllers, middleware, DI composition root.
Controllers must be thin: Validate → MediatR → Return result. No business logic in controllers.

Never introduce architectural changes unless existing architecture blocks implementation,
security requires it, a performance bottleneck is proven, or the user explicitly requests it.
Always explain rationale, tradeoffs, and migration impact when changing architecture.
