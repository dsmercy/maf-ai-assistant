---
language: typescript
category: react-testing
priority: high
applies_to: unit-test
---
# React/TS Testing Standards

Frameworks: Vitest + React Testing Library + MSW.
Validation: `vitest run`

## Test Structure
Arrange / Act / Assert:
```typescript
test('shows error when order creation fails', async () => {
    // Arrange
    server.use(http.post('/api/orders', () => HttpResponse.error()));
    render(<CreateOrderForm />);

    // Act
    await userEvent.click(screen.getByRole('button', { name: /submit/i }));

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent(/something went wrong/i);
});
```

## Naming
`does X when Y` — e.g. `shows loading spinner when fetching orders`, `disables submit button while pending`

## Query Priority (React Testing Library)
Query in this order: `getByRole` → `getByLabelText` → `getByText` → `getByTestId`
Never query by CSS class or internal component implementation details.

## Rules
- Never mock `fetch` or `axios` directly — use MSW to intercept at the network boundary
- Never call real API endpoints in tests
- Test behaviour, not implementation
- Cover: success, error, loading, empty, optimistic update, and rollback states

## Coverage Expectations
Business logic: 90%+. Critical flows: 100%.
