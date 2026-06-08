---
language: python
category: python-testing
priority: high
applies_to: unit-test
---
# Python Testing Standards

Frameworks: pytest + pytest-asyncio + AsyncMock + respx.
Validation: `pytest -x && ruff check . && mypy src/`

## Test Structure
Always Arrange / Act / Assert with section comments:
```python
async def test_creates_order_when_customer_exists():
    # Arrange
    repo = AsyncMock()
    repo.find_customer.return_value = Customer(id="c1")
    service = OrderService(repo)

    # Act
    result = await service.create(CreateOrderRequest(customer_id="c1", items=[]))

    # Assert
    assert result.id is not None
    repo.find_customer.assert_called_once_with("c1")
```

## Naming
`test_does_x_when_y` — e.g. `test_raises_when_customer_not_found`, `test_retries_on_network_error`

## Rules
- Use `AsyncMock` for coroutines
- Use `respx` for mocking HTTP calls (httpx)
- Use `tmp_path` fixture for file system operations — never touch real files
- Never call real Ollama, real databases, or real external services in unit tests
- Never test implementation details — test observable behaviour

## What to Test
- Success paths
- Error paths (typed exceptions raised correctly)
- Validation failures
- Retry behaviour
- Edge cases (empty input, boundary values)
