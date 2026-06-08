---
language: javascript
category: architecture
priority: high
applies_to: code-generation, code-review
---
# Node.js Project Architecture

```
src/
├── api/            routes/, controllers/, middleware/
├── application/    services/, use-cases/
├── domain/         entities/, value-objects/, contracts/
├── infrastructure/ database/, external-services/, repositories/
├── shared/         utils/, errors/, constants/
└── config/
```

API layer: request handling, validation, response generation only.
Must NOT contain: business logic, direct DB access, external service orchestration.

Application layer: use cases, orchestration, workflow coordination.
Must NOT contain: HTTP concerns, framework-specific code.

Domain layer: business rules, entities, domain concepts.
Must remain framework-independent.

Infrastructure layer: DB access, external APIs, caching, messaging.
Implements abstractions defined in domain/application.

## ES Modules
Use ES Modules exclusively. Always use `node:` prefix for built-ins:
```javascript
// Good
import { readFile } from 'node:fs/promises';
// Bad
const fs = require('fs');
import { readFile } from 'fs/promises';
```

## Dependency Injection
Prefer explicit constructor injection — no hidden globals, service locators, or shared mutable state:
```javascript
const orderService = new OrderService(orderRepository, logger);
```
