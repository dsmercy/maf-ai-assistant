---
language: typescript
category: react-observability
priority: medium
applies_to: code-generation
---
# React/TS Observability & Security

## Logging
Use a centralised logging utility — never `console.log()` or `console.error()` in production code.
Capture and log: API failures, React boundary errors, unexpected runtime errors.

## Security Standards
- Validate all user input (Zod on every form and API response)
- Sanitize HTML before rendering — never use `dangerouslySetInnerHTML` without explicit sanitisation and approval
- Use Content Security Policy headers
- Never store in localStorage: JWTs, API secrets, credentials, sensitive user data
- Keep all secrets out of frontend code — use env vars only, never commit

## Code Review Checklist for React/TS
Verify:
- Type safety — no `any`, no unsafe casts
- Architecture compliance — no business logic in components
- Accessibility — semantic HTML, keyboard nav, ARIA
- Error handling — all async states covered
- Test coverage — behaviour tested, not implementation
- Security — no dangerouslySetInnerHTML, no sensitive localStorage usage
- Performance — no unmeasured memoisation
- Bundle size — no unnecessary dependencies

Reject PRs that introduce: `any`, duplicated logic, business logic in components, unvalidated external data, unnecessary dependencies.
