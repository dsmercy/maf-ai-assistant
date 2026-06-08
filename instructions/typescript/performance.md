---
language: typescript
category: react-performance
priority: medium
applies_to: code-generation, code-review
---
# React/TS Performance Standards

## Measure Before Optimising
Do NOT add `React.memo`, `useMemo`, `useCallback` without profiling evidence showing a real problem.
Premature memoisation adds complexity and can cause subtle bugs.

## Required
- Route-level code splitting with `React.lazy` + `Suspense`
- Lazy loading for heavy components and routes
- Bundle size monitoring — review on every significant dependency addition
- Lighthouse review for Core Web Vitals before production release

## What to Monitor
- Bundle size (use `vite-bundle-visualizer` or similar)
- Render counts (React DevTools Profiler)
- Web Vitals: LCP, FID/INP, CLS

## i18n
If localisation is enabled: never hardcode user-facing strings — always use translation keys:
```tsx
<Button>{t("common.save")}</Button>    // Good
<Button>Save</Button>                   // Bad — not translatable
```

## Testing Standards
Frameworks: Vitest + React Testing Library + MSW.
Validation: `tsc --noEmit && eslint src --max-warnings 0 && vitest run && vite build`

Rules:
- Test behaviour, not implementation details
- Query by role, label, text — never by CSS class or internal component name
- Never mock fetch or axios directly — use MSW to intercept at the network level
- Never call real API endpoints

Test all states: Loading, Success, Empty, Error, optimistic updates, rollbacks.
Naming: `does X when Y` — e.g. `shows error message when order creation fails`
