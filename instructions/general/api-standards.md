---
language: general-api
category: api-standards
priority: high
applies_to: code-generation, code-review
---
# API Standards (Cross-Stack)

## Contract Safety

Preserve existing API contracts unless explicitly instructed to change them.
Flag as CRITICAL before changing: response shapes, request payloads, HTTP status codes, validation rules, authentication behaviour.
Always document breaking changes.

## HTTP Status Codes

| Code | Meaning |
|---|---|
| 200 | OK |
| 201 | Created |
| 204 | No Content |
| 400 | Bad Request |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not Found |
| 409 | Conflict |
| 422 | Unprocessable Entity |
| 500 | Internal Server Error |

## API Design Rules

- Always paginate list endpoints — never return unbounded collections
- Use API versioning: prefer `/api/v1/` prefix
- Return structured errors, never plain "Something went wrong"
- C#: return RFC 9457 ProblemDetails
- Node.js: return `{ "code": "X", "message": "Y" }`
- Never expose stack traces, internal details, or database errors to clients
- Validate all requests before business logic executes
- Return minimal required data — avoid excessive exposure

## Configuration Safety

Always highlight changes to: appsettings.json, appsettings.*.json, environment variables, Dockerfiles, docker-compose files, CI/CD pipelines, package.json, Kubernetes manifests.
Explain the operational impact of every configuration change.
