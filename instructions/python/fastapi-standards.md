---
language: python
category: fastapi-standards
priority: high
applies_to: code-generation, code-review
---
# FastAPI Standards

## Route Design
Validate all inputs with Pydantic models — never accept raw `dict`:
```python
@router.post("/orders", response_model=OrderDto)
async def create_order(request: CreateOrderRequest, service: OrderService = Depends(get_order_service)):
    return await service.create(request)
```

## Dependency Injection
Always use `Depends()` for services — never instantiate services directly in route functions.
```python
# Good
async def create_order(service: OrderService = Depends(get_order_service)): ...

# Bad
async def create_order():
    service = OrderService(db)  # never
```

## Error Handling
Use `HTTPException` with `detail` — never expose raw tracebacks to clients:
```python
raise HTTPException(status_code=404, detail=f"Order {order_id} not found")
```

## Streaming
Use `StreamingResponse` for token/data streams — never buffer the full response:
```python
return StreamingResponse(generate_tokens(), media_type="text/event-stream")
```

## API Versioning
Version all routes via `/v1/` prefix: `@router.post("/v1/orders")`

## Configuration
Use `pydantic_settings.BaseSettings` — never hardcode any values:
```python
class Settings(BaseSettings):
    database_url: str
    ollama_base_url: str = "http://localhost:11434"
    model_config = SettingsConfigDict(env_file=".env")
```
