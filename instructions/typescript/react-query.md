---
language: typescript
category: react-query
priority: high
applies_to: code-generation, code-review
---
# React Query Standards

React Query owns: fetching, caching, synchronisation, mutations, background refetching.
It does NOT own: auth state, UI state, user preferences (those belong in Zustand).

## Query Key Factories
Centralise all query keys — never inline them in components:
```typescript
export const orderKeys = {
    all:    ["orders"] as const,
    list:   (filters?: OrderFilters) => [...orderKeys.all, "list", filters] as const,
    detail: (id: string)             => [...orderKeys.all, id] as const,
};
```

## Query Configuration
Always define `staleTime` and retry behaviour explicitly:
```typescript
useQuery({
    queryKey: orderKeys.detail(id),
    queryFn:  () => orderService.getById(id),
    staleTime: 30_000,
    retry: 2,
});
```

## Rules
- Never inline query keys in component files
- Use query key factories for all invalidation
- Use optimistic updates only when justified — they add complexity
- Invalidate using query key factories, never string literals

## Mutations
Use `useMutation` with `onSuccess` invalidation:
```typescript
const { mutate } = useMutation({
    mutationFn: orderService.cancel,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: orderKeys.all }),
});
```
