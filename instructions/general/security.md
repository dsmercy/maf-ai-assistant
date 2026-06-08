---
language: general-security
category: security
priority: high
applies_to: code-review, code-generation
---
# Security Standards (Cross-Stack)

Security takes precedence over convenience, performance, refactoring preferences, and architectural preferences.
Never introduce a security regression to satisfy another requirement.

Flag any security concern with WARNING before implementation. Explain: risk, impact, recommended mitigation.

## OWASP Top 10 — always review against these

1. Broken Access Control
2. Cryptographic Failures
3. Injection (SQL, Command, Code)
4. Insecure Design
5. Security Misconfiguration
6. Vulnerable and Outdated Components
7. Identification and Authentication Failures
8. Software and Data Integrity Failures
9. Security Logging and Monitoring Failures
10. Server-Side Request Forgery (SSRF)

## Input Validation

Validate ALL external input: HTTP requests, query params, route params, form submissions, env vars, uploaded files, message queues, external APIs.
Never trust client-supplied data.

## Injection Protection

SQL: always parameterized queries — never concatenate user input into SQL.
Command: never pass user input into shell commands, process execution, or system utilities.
Code: never use eval() or dynamic code execution with user-controlled input.

## Authentication & Authorisation

Validate: token signatures, expiration, issuer, audience. Never trust client-provided identity.
Authentication does not imply authorisation. Always verify resource ownership and permissions server-side.
Apply least privilege. Deny by default. Never rely solely on UI restrictions.

## Secrets Management

Store secrets only in: environment variables, secret managers (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault).
Never: commit secrets, hardcode credentials, store in source code or test fixtures, return secrets in API responses.
Never log: passwords, tokens, API keys, connection strings, PII, financial or health information.

## File Upload Security

Validate: file size, extension, MIME type, upload destination. Never trust client-supplied file names or MIME types.
Store uploads outside executable paths. Restrict executable content.

## File System Security

Protect against path traversal. Canonicalize and validate all paths before file operations.
Never trust file paths from users. Use Path.GetFullPath() (.NET) or equivalent safe helpers.

## SSRF Protection

Validate all outbound network request destinations. Restrict protocols and internal addresses. Use allow lists where possible.
Never fetch arbitrary user-provided URLs without validation.

## Output Encoding

Encode all user-controlled output. Prefer framework-provided encoding. Avoid bypassing built-in protections (e.g. dangerouslySetInnerHTML in React).

## Cryptography

Never implement custom cryptography. Avoid MD5, SHA1, custom encryption.
Prefer: AES-256, SHA-256, PBKDF2, Argon2, bcrypt. Review key storage and rotation.
Never store plaintext passwords. Use Argon2, bcrypt, or PBKDF2 with appropriate work factors.

## API Security

Review for: broken authorisation, excessive data exposure, mass assignment, resource enumeration, missing rate limiting, missing validation.
Return minimal required data. Paginate list endpoints. Protect sensitive operations.

## Session Security

Require: Secure cookies, HttpOnly, SameSite protection, session expiration.
Protect against session fixation and hijacking.

## Security Headers

Flag if missing: Content-Security-Policy, Strict-Transport-Security, X-Content-Type-Options, Referrer-Policy, Permissions-Policy.

## CORS

Never use wildcard (*) origins in production without explicit justification. Define allowed origins, methods, and headers explicitly.

## Rate Limiting

Protect: login, registration, password reset, and public API endpoints from brute-force, enumeration, and resource exhaustion.

## Dependency Security

Before adding: verify maintenance status, licensing, known CVEs, project activity, supply-chain risks.
Run: `dotnet list package --vulnerable` / `npm audit` / `pip-audit` regularly.

## Container & Infrastructure Security

Containers: prefer non-root users, minimal base images, pinned image versions (never `FROM image:latest`).
Infrastructure: prefer private networking, network segmentation, zero-trust principles.

## Multi-Tenant Security

Always verify tenant ownership and filtering. Never trust tenant identifiers from clients. Explicitly prevent cross-tenant access.

## Security Logging

Log: login failures, authorisation failures, permission denials, token validation failures.
Security-relevant actions must be traceable with correlation IDs and user identifiers.

## Universal Rules Quick Reference

| Vulnerability | Rule |
|---|---|
| SQL Injection | Parameterized queries only |
| XSS | Encode output, avoid unsafe rendering |
| Path Traversal | Canonicalize and validate paths |
| SSRF | Validate outbound destinations |
| Secrets Exposure | Use secret stores only |
| Missing Auth | Secure endpoints by default |
| Broken Authorisation | Verify ownership and permissions |
| Command Injection | Never execute unsanitized input |
| Sensitive Logging | Redact sensitive values |
| Open CORS | Explicit origin allow lists |
| Weak Cryptography | Use approved algorithms only |
