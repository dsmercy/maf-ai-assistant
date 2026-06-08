---
language: general-testing
category: testing
priority: medium
applies_to: unit-test
---
# Testing Standards (Cross-Stack)

## When to generate tests

Generate tests when: behaviour changes, business logic changes, bugs are fixed, new features are introduced.
Recommend tests when existing coverage appears insufficient.
Do NOT generate tests for: formatting changes, documentation updates, non-functional comments.

## Test structure

Always use Arrange / Act / Assert sections.

## Naming conventions

| Stack | Pattern |
|---|---|
| C# / xUnit | `MethodName_Scenario_ExpectedBehaviour` |
| Node.js / Python / React | `does_x_when_y` or `test_does_x_when_y` |

## Unit test rules

- Test one behaviour per test
- Never call real HTTP endpoints or real databases in unit tests
- Mock all external boundaries (HTTP, DB, file system)
- Test: success paths, error paths, validation failures, edge cases

## Integration test rules

- Use integration tests for: APIs, authentication, authorisation, database, infrastructure integrations
- Do not mock infrastructure that is under test

## Coverage expectations

Business logic: 90%+
Critical workflows: 100%

## Stack-specific frameworks

| Stack | Frameworks |
|---|---|
| C# | xUnit + Moq + FluentAssertions |
| Node.js | Vitest / Jest + Supertest |
| React/TS | Vitest + React Testing Library + MSW |
| Python | pytest + pytest-asyncio + AsyncMock + respx |

## Validation commands

```bash
dotnet test                        # C#
npm test                           # Node.js
vitest run                         # React/TS
pytest -x                          # Python
```

Never claim a test passed unless it was actually executed.
