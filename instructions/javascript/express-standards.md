---
language: javascript
category: express-standards
priority: high
applies_to: code-generation, code-review
---
# Express.js Standards

## Controller Design
Controllers must only: 1) validate input, 2) call service/use-case, 3) return response.
Never put business logic, SQL queries, or external API calls in controllers.

```javascript
// Good
async function createOrderController(req, res, next) {
    const parsed = CreateOrderSchema.safeParse(req.body);
    if (!parsed.success) return res.status(400).json({ errors: parsed.error.issues });

    const id = await orderService.create(parsed.data);
    res.status(201).json({ id });
}

// Bad — 200+ lines of logic in route handler
router.post('/orders', async (req, res) => { /* ... */ });
```

## Route Registration
```javascript
router.post('/orders', createOrderController);  // Good
```

## Middleware
Use middleware for: authentication, authorisation, logging, validation, correlation IDs.
No business logic in middleware.

## Error Handling
Use a single global error middleware — avoid repetitive try/catch blocks throughout routes:
```javascript
app.use(errorHandler);
```
Never expose stack traces, internal details, or database errors to clients.
Return consistent error shapes: `{ "code": "ORDER_NOT_FOUND", "message": "Order not found" }`

## Validation
Validate all external input with Zod before business logic executes:
```javascript
const CreateOrderSchema = z.object({ customerId: z.string().uuid() });
```
Never trust: `req.body`, `req.query`, `req.params`, environment variables, external API responses.
