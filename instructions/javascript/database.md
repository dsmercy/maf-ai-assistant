---
language: javascript
category: nodejs-database
priority: high
applies_to: code-generation, code-review
---
# Node.js Database Access

## SQL Injection Prevention
Always use parameterized queries or query builders. Never concatenate user input into SQL:
```javascript
// Bad — SQL injection risk
const sql = `SELECT * FROM Users WHERE Id = ${id}`;

// Good — parameterized
db.query('SELECT * FROM Users WHERE Id = ?', [id]);
// or with a query builder
db.select('*').from('Users').where('Id', id);
```

## General Rules
- Use ORMs or query builders to avoid raw string SQL
- Validate all query inputs before executing
- Never expose database error messages to API clients
- Keep database access in the infrastructure layer — never in controllers or domain logic
- Use connection pooling — never create a new connection per request
