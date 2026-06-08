---
language: general-database
category: database-safety
priority: high
applies_to: code-generation, code-review
---
# Database Safety (Cross-Stack)

Never:
- Drop data without explicit instruction
- Remove migrations without justification
- Modify production data assumptions silently
- Introduce destructive changes without flagging them

Always:
- Explain migration impact before applying
- Highlight rollback considerations
- Preserve backward compatibility where possible

Flag data loss risks as CRITICAL before proceeding.

## SQL Injection Prevention

Always use parameterized queries. Never concatenate user input into SQL strings.

Bad:
```sql
SELECT * FROM Users WHERE Id = ' + userInput
```
Good:
```sql
SELECT * FROM Users WHERE Id = @Id   -- C# / EF Core
db.query('SELECT * FROM Users WHERE Id = ?', [id])  -- Node.js
```

## EF Core specific rules

- Always use `AsNoTracking()` for read-only queries
- Project to DTOs — never return tracked entities from controllers
- Prevent N+1: use Include/ThenInclude appropriately
- Use split queries when necessary to avoid cartesian explosions
- Use optimistic concurrency — handle `DbUpdateConcurrencyException` explicitly
- Never allow accidental client-side query evaluation
- Review every migration manually before applying to production

## Transaction rules

Commands own transaction boundaries. Queries must remain read-only.
Avoid long-running transactions and nested transactions. Keep transactions short.
