---
language: typescript
category: state-management
priority: high
applies_to: code-generation, code-review
---
# React State Management

## Zustand — Client State Only
Zustand owns: authentication state, theme, user preferences, UI-only state.
Zustand does NOT own: server data, API caching (that belongs in React Query).

Rules:
- Stores contain state and actions only — no API calls inside stores
- Use selectors for subscriptions to avoid unnecessary re-renders
- Use slices for large stores
```typescript
const useOrderStore = create<OrderState>()((set) => ({
    selectedId: null,
    setSelectedId: (id) => set({ selectedId: id }),
}));

// Selector — subscribe only to what you need
const selectedId = useOrderStore((s) => s.selectedId);
```

## Component State Rules
- Use `useState` for purely local, isolated UI state
- Use `useMemo` / `useCallback` ONLY after profiling shows measurable benefit — not by default
- Use `useEffect` only for genuine side effects (subscriptions, DOM interaction)
- Never use `useEffect` for derived state, data transformations, or computed values

## When to Use Each
| Data | Tool |
|---|---|
| Server / API data | React Query |
| Shared UI / auth state | Zustand |
| Local component state | useState |
| Expensive computation | useMemo (only if profiled) |
