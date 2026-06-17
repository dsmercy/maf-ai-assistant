# Admin Guide

Day-to-day administration of the Enterprise Local AI Coding Assistant.

---

## API base URLs

| Service | URL |
|---|---|
| Assistant API | http://localhost:5000 |
| Open WebUI | http://localhost:3000 |
| Qdrant dashboard | http://localhost:6333/dashboard |
| Ollama API | http://localhost:11434 |

---

## Repository management

### Register a repository for indexing

```powershell
curl -X POST http://localhost:5000/api/repositories `
  -H "Content-Type: application/json" `
  -d '{"url":"https://github.com/owner/repo","branch":"main"}'
```

For private repositories, include a Personal Access Token (PAT):

```powershell
curl -X POST http://localhost:5000/api/repositories `
  -H "Content-Type: application/json" `
  -d '{"url":"https://github.com/owner/private-repo","branch":"main","pat":"ghp_xxxxxxxxxxxx"}'
```

### List all repositories

```powershell
curl http://localhost:5000/api/repositories
```

Response fields:
- `status` — `Pending`, `Indexing`, `Indexed`, `Failed`
- `fileCount` — number of files indexed
- `chunkCount` — number of vector chunks stored
- `lastIndexedAt` — timestamp of last successful indexing

### Delete a repository

Removes the registration, all Qdrant vectors, and file hashes:

```powershell
curl -X DELETE http://localhost:5000/api/repositories/<id>
```

### Re-index a repository

Clears all existing vectors and queues a fresh indexing job:

```powershell
curl -X POST http://localhost:5000/api/repositories/<id>/reindex
```

### Upload a ZIP archive

```powershell
curl -X POST http://localhost:5000/api/repositories/upload `
  -F "file=@path/to/repo.zip"
```

---

## Document management

### Upload a document (PDF, DOCX, TXT, MD)

Stored in `doc-embeddings` collection:

```powershell
curl -X POST http://localhost:5000/api/documents `
  -F "file=@path/to/document.pdf"
```

### Upload instruction / coding standards files

Stored in `instruction-embeddings` collection. Files must use Markdown format with
YAML front matter to enable language filtering:

```markdown
---
language: csharp
category: ef-core
---
# EF Core Standards

- Always use AsNoTracking() on read-only queries
- Never return tracked entities from controllers — project to DTOs
```

Upload command:

```powershell
curl -X POST http://localhost:5000/api/instructions `
  -F "file=@instructions/csharp/ef-core.md"
```

Supported `language` values in front matter:

| language tag | Applies to |
|---|---|
| `csharp` | C#, .NET, ASP.NET Core |
| `typescript` | TypeScript, React, Angular |
| `javascript` | JavaScript, Node.js, Vue |
| `python` | Python |
| `go` | Go |
| `java` | Java |
| `rust` | Rust |

If no front matter is present, the instruction file is returned for any language query.

---

## Ingestion job monitoring

### List all jobs

```powershell
curl http://localhost:5000/api/jobs
```

Response includes:
- `total`, `queued`, `running`, `completed`, `failed` counts
- Array of recent jobs with status, timestamps, and error messages

### Get a specific job

```powershell
curl http://localhost:5000/api/jobs/<job-id>
```

### Job lifecycle

```
Queued → Running → Completed
                 → Failed
```

- `Queued` — waiting for the IngestionWorker to pick it up
- `Running` — currently being processed (polls every 10 seconds)
- `Completed` — successfully indexed
- `Failed` — error occurred; check `errorMessage` field

### Stuck jobs

If AssistantApi crashes while a job is `Running`, those jobs are automatically
reset to `Queued` when the service restarts. To manually reset stuck jobs, restart the
assistant-api container:

```powershell
docker compose restart assistant-api
```

---

## Prompt template management

Prompt templates control how the CodingAgent formats its prompt for each intent type.

### List all templates

```powershell
curl http://localhost:5000/api/admin/prompt-templates
```

### Get a specific template

Valid task types: `CodeGeneration`, `CodeExplanation`, `CodeReview`, `UnitTest`,
`Documentation`, `RepositoryQuestion`, `GeneralQuestion`

```powershell
curl http://localhost:5000/api/admin/prompt-templates/CodeGeneration
```

### Update a template

```powershell
curl -X PUT http://localhost:5000/api/admin/prompt-templates/CodeGeneration `
  -H "Content-Type: application/json" `
  -d '{
    "systemPrompt": "You are an expert software engineer.\n\nCoding standards:\n{instructions}\n\nRelevant code context:\n{context_chunks}\n\nUser request:\n{user_message}\n\nLanguage: {language}\n\nRespond with clean, production-ready code."
  }'
```

Available placeholders in templates:

| Placeholder | Replaced with |
|---|---|
| `{instructions}` | Rules retrieved from instruction-embeddings |
| `{context_chunks}` | Code chunks retrieved from code-embeddings |
| `{user_message}` | The user's original question |
| `{language}` | Detected programming language |

---

## Feature flag management

Feature flags control runtime behaviour without redeployment.

### List all flags

```powershell
curl http://localhost:5000/api/admin/feature-flags
```

### Get a specific flag

```powershell
curl http://localhost:5000/api/admin/feature-flags/streaming
```

### Enable or disable a flag

```powershell
# Enable streaming
curl -X PUT http://localhost:5000/api/admin/feature-flags/streaming `
  -H "Content-Type: application/json" `
  -d '{"isEnabled": true}'

# Disable RAG (answer without code context — faster, less accurate)
curl -X PUT http://localhost:5000/api/admin/feature-flags/rag `
  -H "Content-Type: application/json" `
  -d '{"isEnabled": false}'
```

### Default feature flags

| Flag | Default | Effect when disabled |
|---|---|---|
| `streaming` | `true` | Responses returned in one batch |
| `rag` | `true` | No code context retrieved — raw LLM only |
| `auth` | `false` | All endpoints are unauthenticated |
| `audit` | `true` | No audit log written |
| `rate-limit` | `true` | No rate limiting enforced |

---

## Audit log

All API calls are recorded in the audit log.

### View recent entries

```powershell
curl http://localhost:5000/api/admin/audit-logs
```

Or query PostgreSQL directly:

```powershell
docker exec postgres psql -U assistant -d assistant_db `
  -c "SELECT created_at, user_id, method, path, status_code, duration_ms FROM \"AuditLogs\" ORDER BY created_at DESC LIMIT 50;"
```

---

## Semantic search

Test what the vector index returns for a given query:

```powershell
# Search code chunks
curl "http://localhost:5000/api/search?q=dependency+injection&collection=code-embeddings&topK=5"

# Search with repository filter
curl "http://localhost:5000/api/search?q=repository+pattern&collection=code-embeddings&repository=my-repo&topK=5"

# Search instruction rules
curl "http://localhost:5000/api/search?q=error+handling&collection=instruction-embeddings&topK=5"

# Search with language filter
curl "http://localhost:5000/api/search?q=async+await&collection=code-embeddings&language=csharp&topK=5"
```

---

## Qdrant collection management

### View all collections

```powershell
curl http://localhost:6333/collections
```

### View collection details (vector count, disk usage)

```powershell
curl http://localhost:6333/collections/code-embeddings
curl http://localhost:6333/collections/doc-embeddings
curl http://localhost:6333/collections/instruction-embeddings
```

### Delete a collection (destructive — removes all vectors)

```powershell
curl -X DELETE http://localhost:6333/collections/code-embeddings
```

The API will recreate the collection automatically on the next ingestion job.

---

## Ollama model management

### List loaded models

```powershell
docker exec ollama ollama list
```

### Pull a new model

```powershell
docker exec ollama ollama pull qwen3-coder:14b
```

### Remove a model (frees disk space)

```powershell
docker exec ollama ollama rm qwen3-coder:30b
```

### Switch the active chat model

Update in `docker-compose.yml` under `assistant-api`:

```yaml
environment:
  Assistant__ChatModel: "qwen3-coder:14b"
```

Then recreate the container:

```powershell
docker compose up -d --force-recreate assistant-api
```

---

## Health checks

```powershell
# Liveness (is the process running?)
curl http://localhost:5000/health

# Readiness (are all downstream services reachable?)
curl http://localhost:5000/health/ready
```

The readiness check verifies:
- PostgreSQL can accept a query
- Qdrant responds on gRPC port 6334
- Ollama responds on port 11434

---

## Viewing logs

```powershell
# All services
docker compose logs -f

# Specific service
docker compose logs -f assistant-api
# Ingestion worker logs are part of assistant-api
docker compose logs -f assistant-api

# Last 100 lines
docker compose logs --tail=100 assistant-api
```

Logs are structured JSON (Serilog). To filter by level:

```powershell
docker compose logs assistant-api | Select-String '"Level":"Error"'
docker compose logs assistant-api | Select-String '"Level":"Warning"'
```

---

## Conversation history

### List conversations for a user

```powershell
curl "http://localhost:5000/api/conversations?userId=<user-id>"
```

### Get a specific conversation

```powershell
curl http://localhost:5000/api/conversations/<conversation-id>
```
