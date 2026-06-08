---
language: typescript
category: react-components
priority: high
applies_to: code-generation, code-review
---
# React Component Standards

## Component Rules
- Named exports only — no default exports for components
- One responsibility per component — keep small and composable
- Prefer composition over prop drilling
- No business logic inside components
- No direct API calls from components

```typescript
// Good — named export, single responsibility
export function OrderCard({ order }: { order: OrderDto }) {
    return <div>{order.id}</div>;
}

// Bad — business logic in component, default export
export default function OrderCard({ id }: { id: string }) {
    const [order, setOrder] = useState(null);
    useEffect(() => { fetch(`/api/orders/${id}`).then(...) }, []);  // never
}
```

## Custom Hooks
Extract data fetching, mutations, and reusable stateful behaviour into custom hooks:
```typescript
function useOrder(id: string) {
    return useQuery({ queryKey: orderKeys.detail(id), queryFn: () => orderService.getById(id) });
}
```

## Forms
Use React Hook Form + Zod resolver. All forms must:
- Validate client-side (Zod) and server-side
- Disable submit button while pending
- Display accessible error messages with `aria-invalid` and `role="alert"`

## Error Handling in Components
- Use error boundaries for route-level and isolated UI section failures
- Map all errors through a `toUserMessage(error)` utility
- Every async workflow must handle: Loading, Success, Empty, Error states
- Never display raw stack traces or API error messages to users
