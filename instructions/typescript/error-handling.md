---
language: typescript
category: react-error-handling
priority: high
applies_to: code-generation, code-review
---
# React/TS Error Handling

## Error Boundaries
Use error boundaries at two levels:
1. Route level — catches failures in entire page/feature sections
2. Component level — isolates failures in independent UI widgets

```tsx
<ErrorBoundary fallback={<ErrorPage />}>
    <OrdersFeature />
</ErrorBoundary>
```

## Error Message Mapping
Always map errors through a central utility before displaying to users:
```typescript
function toUserMessage(error: unknown): string {
    if (error instanceof ApiError) return error.userMessage;
    if (error instanceof ZodError) return "Invalid input. Please check your data.";
    return "Something went wrong. Please try again.";
}
```
Never display: raw stack traces, raw API error messages, internal field names, server error codes.

## Async State Handling
Every async workflow must explicitly handle all four states:
```tsx
if (isLoading) return <Spinner />;
if (isError)   return <ErrorMessage error={toUserMessage(error)} />;
if (!data)     return <EmptyState />;
return <OrderList orders={data} />;
```

## Form Validation Errors
Display field errors accessibly:
```tsx
<input aria-invalid={!!errors.email} aria-describedby="email-error" />
{errors.email && <span id="email-error" role="alert">{errors.email.message}</span>}
```
