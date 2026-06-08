---
language: typescript
category: architecture
priority: high
applies_to: code-generation, code-review
---
# React / TypeScript Project Architecture

## Feature-First Structure
Organise by business feature, not technical layer:
```
src/
├── app/          router/, providers/, layouts/
├── features/
│   └── orders/   api/, hooks/, components/, schemas/, types/, pages/
├── shared/       components/, hooks/, utils/, types/, constants/
├── services/
└── assets/
```
Avoid flat `components/ hooks/ pages/` for large applications.

## Separation of Concerns
- Components: render UI only
- Hooks: manage behaviour and stateful logic
- Services: API communication
- Utils: pure reusable functions
- No business logic inside components

## TypeScript Strict Mode
Enable strict mode. Type all props, hooks, API responses, stores, utilities.
Prefer type aliases over interfaces unless extension is needed.
Use discriminated unions for state modelling. Use string unions instead of enums:
```typescript
type Status = "active" | "inactive" | "pending";
```
Forbidden: `any`, `ts-ignore` without explanation, implicit any, unsafe type assertions.
Use `unknown` + proper type narrowing instead.

## Naming Conventions
| Item | Convention |
|---|---|
| Components | PascalCase |
| Hooks | `useSomething` |
| Types/DTOs | `UserDto` |
| Constants | `UPPER_SNAKE_CASE` |
| Files | `kebab-case` |
| React Query keys | `entityKeys` |
| Zustand stores | `useSomethingStore` |

## Import Standards
Use path aliases — never relative imports deeper than two levels:
```typescript
import { Button } from "@/shared/components/button";  // Good
import Button from "../../../../components/Button";     // Bad
```
