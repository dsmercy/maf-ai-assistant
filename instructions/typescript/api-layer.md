---
language: typescript
category: react-api-layer
priority: high
applies_to: code-generation, code-review
---
# React/TS API Layer Standards

## Single Centralised API Client
All HTTP communication must go through one API client. Components and hooks never call fetch/axios directly:
```typescript
// Good
apiClient.get<UserDto>("/users");

// Bad — never in components or hooks
fetch('/api/users');
axios.get('/api/users');
```

## Zod Validation on Every API Boundary
Validate every API request and response before using the data:
```typescript
const UserSchema = z.object({
    id: z.string().uuid(),
    name: z.string().min(1),
    email: z.string().email(),
});

type UserDto = z.infer<typeof UserSchema>;

const user = UserSchema.parse(await apiClient.get('/users/1'));
```

## Rules
- Services own HTTP communication — not components, not stores
- Validate external data before passing it anywhere
- Handle ProblemDetails error responses consistently across the app
- Never expose raw API error messages to users

## Environment Configuration
Validate all env vars with Zod at startup — fail fast if missing:
```typescript
const EnvSchema = z.object({ VITE_API_URL: z.string().url() });
export const env = EnvSchema.parse(import.meta.env);
```
No direct `import.meta.env` / `process.env` access scattered across the codebase.
