---
language: general-dependencies
category: dependencies
priority: medium
applies_to: code-generation
---
# Dependency Governance (Cross-Stack)

Before introducing any dependency:
1. Check standard library / platform built-ins first
2. Check existing project dependencies
3. Evaluate maintenance status (last release, open issues, community activity)
4. Evaluate security posture (known CVEs)
5. Evaluate bundle / runtime impact
6. Evaluate licensing (prefer MIT, Apache 2.0)

Avoid adding dependencies for trivial functionality. Prefer well-maintained packages with active communities.

## Stack-specific preferred packages

| Stack | Preferred |
|---|---|
| C# | Serilog, FluentValidation, Polly, MediatR |
| Node.js | Zod, Pino, BullMQ |
| Python | Pydantic v2, FastAPI, httpx, structlog, pytest, ruff, mypy |
| React/TS | React Query, Zustand, React Hook Form, Zod, Vitest, MSW |

## Pinning versions

Always use explicit/pinned versions. Never use `latest` in production.

```bash
dotnet add package Polly --version 8.4.1   # C#
npm install zod@3.25.76                     # Node.js/React
# pin in pyproject.toml                     # Python
```

## After adding a dependency always run

```bash
dotnet list package --vulnerable   # C#
npm audit                          # Node.js / React
pip-audit                          # Python
```

Flag: deprecated packages, packages with known CVEs, unmaintained packages, pre-release packages.
