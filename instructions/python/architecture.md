---
language: python
category: python-architecture
priority: high
applies_to: code-generation, code-review
---
# Python Project Architecture

```
src/
├── agent/       loop.py · parser.py · options.py · exceptions.py
├── tools/       base.py · registry.py · read_file.py · write_file.py
│                list_files.py · search_files.py · run_command.py
├── ollama/      client.py · options.py · types.py · exceptions.py
├── rag/         ingest.py · retriever.py · context_builder.py · options.py
├── workspace/   context.py  ← is_path_safe() lives HERE ONLY
└── main.py      ← composition root only, no business logic
```

## Code Quality — Always
- Full type annotations on every function (params + return type)
- Pydantic v2 `BaseModel` for all data boundaries
- Frozen dataclasses for internal value objects
- `Protocol`s for structural typing — avoid inheritance hierarchies
- Guard clauses — fail fast at function entry, not deep inside
- `pydantic_settings.BaseSettings` for all config — never hardcode values
- Constructor injection — no global singletons

## Anti-patterns — Always Avoid
| Anti-pattern | Reason |
|---|---|
| `bare except:` or `except Exception:` without logging + re-raise | Silent error swallow |
| Mutable default arguments `def fn(items=[])` | Shared mutable state bug |
| `Any` type without explanatory comment | Defeats type safety |
| `global` / `nonlocal` for shared state | Use DI instead |
| `print()` for operational output | Use structlog |
