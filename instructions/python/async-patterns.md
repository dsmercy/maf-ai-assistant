---
language: python
category: python-async
priority: high
applies_to: code-generation, code-review
---
# Python Async & Performance

## HTTP Clients
Use `httpx.AsyncClient` for ALL HTTP calls in async code — never `requests` library:
```python
# Good
async with httpx.AsyncClient() as client:
    response = await client.get(url)

# Bad — blocks event loop
import requests
response = requests.get(url)
```

## Concurrency
Use `asyncio.Semaphore` for bounded concurrency — never unbounded parallel workloads:
```python
semaphore = asyncio.Semaphore(10)

async def process(item):
    async with semaphore:
        return await do_work(item)

results = await asyncio.gather(*[process(item) for item in items])
```

## File I/O
Use `aiofiles` for async file operations — never blocking file I/O in async functions:
```python
async with aiofiles.open(path, 'r') as f:
    content = await f.read()
```

## Anti-patterns
| Anti-pattern | Reason |
|---|---|
| Synchronous blocking I/O inside async functions | Blocks event loop |
| `time.sleep()` in async code | Use `await asyncio.sleep()` |
| Repeated iteration over same generator | Hidden performance cost |
| Buffering full streaming responses | Defeats streaming purpose |

## Streaming
Yield tokens/chunks immediately — never buffer and return all at once:
```python
async def generate():
    async for chunk in llm.stream(prompt):
        yield chunk
```
