---
language: python
category: python-error-handling
priority: high
applies_to: code-generation, code-review
---
# Python Error Handling

## Typed Exceptions
Use typed, descriptive exception classes — never raise generic `Exception`:
```python
class OrderNotFoundError(Exception):
    def __init__(self, order_id: str) -> None:
        super().__init__(f"Order '{order_id}' not found")
        self.order_id = order_id

class OrderAlreadyCancelledError(Exception): ...
```

## Result Pattern
For expected failures (not exceptional cases), use a result type:
```python
type Result[T] = Ok[T] | Err  # Python 3.12+
# or use a dataclass for 3.11:
@dataclass
class Ok(Generic[T]): value: T
@dataclass
class Err: message: str
```

## Never Swallow Exceptions
Always log + re-raise or wrap with context:
```python
# Good
except DatabaseError as e:
    logger.error("db_query_failed", error=str(e), order_id=order_id)
    raise OrderRepositoryError("Failed to load order") from e

# Bad — silent swallow
except Exception:
    pass
```

## Retry
Use `tenacity` for bounded retry — never infinite loops:
```python
@retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=1, max=10))
async def call_external_api(): ...
```

## FastAPI Errors
`HTTPException` with `detail` only — never expose internal tracebacks to API clients.
