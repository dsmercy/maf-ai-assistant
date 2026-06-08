---
language: typescript
category: accessibility
priority: high
applies_to: code-generation, code-review
---
# React Accessibility Standards

## Semantic HTML First
Use native HTML elements before ARIA roles:
```tsx
<button onClick={handleClick}>Submit</button>   // Good
<div onClick={handleClick}>Submit</div>          // Bad — not keyboard accessible

<a href="/orders">View Orders</a>               // Good — for navigation
<span onClick={nav}>View Orders</span>           // Bad
```

## Requirements
- All interactive elements must be keyboard accessible
- Every input must have an associated visible label
- Visible focus indicators — never `outline: none` without a replacement
- Screen reader support via proper ARIA attributes where semantic HTML is insufficient

## Form Accessibility
```tsx
<label htmlFor="email">Email</label>
<input
    id="email"
    aria-invalid={!!errors.email}
    aria-describedby="email-error"
/>
{errors.email && (
    <span id="email-error" role="alert">{errors.email.message}</span>
)}
```

## Modal / Dialog Requirements
Every modal must have:
- Focus trap (focus stays inside while open)
- ESC key closes the modal
- `aria-modal="true"` and `role="dialog"`
- Focus restored to trigger element on close

## Tailwind CSS
Prefer utility-first styling. Use `clsx` + `tailwind-merge` for conditional classes:
```typescript
import { clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';
const cn = (...inputs) => twMerge(clsx(inputs));
```
Avoid massive class strings. Extract repeated patterns into reusable components.
