---
language: javascript
category: error-handling
priority: high
applies_to: code-generation, code-review
---
# Node.js Error Handling

## General Rules
Never ignore errors. Always: log with context, return meaningful responses, preserve debugging info internally.

## Typed Errors
Use typed error classes for domain failures:
```javascript
export class OrderNotFoundError extends Error {
    constructor(orderId) {
        super(`Order ${orderId} not found`);
        this.name = 'OrderNotFoundError';
        this.code = 'ORDER_NOT_FOUND';
    }
}
```

## Global Error Middleware
Use one centralised error handler — avoid repetitive try/catch blocks everywhere:
```javascript
app.use((err, req, res, next) => {
    logger.error({ err, requestId: req.id }, 'Unhandled error');
    res.status(err.statusCode ?? 500).json({
        code: err.code ?? 'INTERNAL_ERROR',
        message: err.message
    });
});
```

## What Never to Expose to Clients
- Stack traces
- Internal error messages
- Database error details
- File paths or system information

## API Error Response Shape
Always return a consistent structure:
```json
{ "code": "ORDER_NOT_FOUND", "message": "Order 123 was not found" }
```
