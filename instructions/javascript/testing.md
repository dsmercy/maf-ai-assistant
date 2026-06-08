---
language: javascript
category: nodejs-testing
priority: high
applies_to: unit-test
---
# Node.js Testing Standards

Frameworks: Vitest (preferred) or Jest + Supertest for API integration tests.
Validation: `npm test`

## Test Structure
Always Arrange / Act / Assert:
```javascript
test('creates order when customer exists', async () => {
    // Arrange
    const repo = { findCustomer: vi.fn().mockResolvedValue({ id: 'c1' }) };
    const service = new OrderService(repo);

    // Act
    const result = await service.create({ customerId: 'c1', items: [] });

    // Assert
    expect(result.id).toBeDefined();
    expect(repo.findCustomer).toHaveBeenCalledWith('c1');
});
```

## Naming
`does X when Y` pattern: `creates order when customer is valid`, `throws when customer not found`

## Unit Test Rules
- Mock all external boundaries (HTTP, DB, file system, queues)
- Never call production systems or external services
- Test: success paths, error paths, validation failures, edge cases

## Coverage Expectations
Business logic: 90%+. Critical workflows: 100%.
