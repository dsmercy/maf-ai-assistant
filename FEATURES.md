# AI Coding Assistant — Feature Catalog

> Self-hosted AI coding assistant built on .NET 10, Ollama, Qdrant, and PostgreSQL.
> Last updated: 2026-06-14

---

## Table of Contents

1. [API Endpoints](#1-api-endpoints)
2. [Agent Pipeline](#2-agent-pipeline)
3. [Intent Classification](#3-intent-classification)
4. [Routing System](#4-routing-system)
5. [Message Bus (Agent Events)](#5-message-bus-agent-events)
6. [Ingestion & RAG Pipeline](#6-ingestion--rag-pipeline)
7. [Vector Search](#7-vector-search)
8. [LLM Integration (Ollama)](#8-llm-integration-ollama)
9. [Infrastructure Integrations](#9-infrastructure-integrations)
10. [Authentication & Authorization](#10-authentication--authorization)
11. [Rate Limiting](#11-rate-limiting)
12. [Health Checks](#12-health-checks)
13. [Logging & Observability](#13-logging--observability)
14. [Validation & Error Handling](#14-validation--error-handling)
15. [Audit Logging](#15-audit-logging)
16. [Feature Flags](#16-feature-flags)
17. [Prompt Templates](#17-prompt-templates)
18. [Startup Automation](#18-startup-automation)
19. [Streaming Support](#19-streaming-support)
20. [OpenAI-Compatible API](#20-openai-compatible-api)
21. [VS Code Extension & Tool Calling](#21-vs-code-extension--tool-calling)
22. [Configuration Reference](#22-configuration-reference)
23. [Docker & Deployment](#23-docker--deployment)
24. [Data Models](#24-data-models)
25. [Architecture Patterns](#25-architecture-patterns)
26. [Performance Optimizations](#26-performance-optimizations)
27. [Resilience & Graceful Degradation](#27-resilience--graceful-degradation)
28. [What Is NOT Implemented](#28-what-is-not-implemented)

---

## 1. API Endpoints

### Chat (`/api/chat`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/chat` | Full agent pipeline — returns complete response with intent, latency, source references |
| POST | `/api/chat/stream` | Server-Sent Events streaming — yields tokens as `data: {"token":"..."}`, ends with `data: [DONE]` |

**Request:** `{ message, conversationId, repositoryFilter?, stream, messagesOverride? }`
**Response:** `{ conversationId, response, intent, latencyMs, sources[] }`

---

### Repositories (`/api/repositories`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/repositories` | List all indexed repositories with status, file count, chunk count, last indexed time |
| GET | `/api/repositories/{id}` | Single repository metadata |
| POST | `/api/repositories` | Register a Git repo (HTTPS/SSH, optional PAT). Creates queued IngestionJob |
| POST | `/api/repositories/upload` | Upload ZIP archive of a repository |
| DELETE | `/api/repositories/{id}` | Delete repository — removes Qdrant vectors, FileHash rows, metadata |
| POST | `/api/repositories/{id}/reindex` | Delete and re-register, triggers fresh ingestion |

---

### Documents & Instructions (`/api/documents`, `/api/instructions` or `/api/documents/instructions`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/documents` | Upload PDF, DOCX, Markdown, or TXT → ingested into `doc-embeddings` |
| POST | `/api/documents/instructions` | Upload coding standards / rules file → ingested into `instruction-embeddings` with LLM categorization |

**Accepted file types:** `.pdf`, `.docx`, `.md`, `.markdown`, `.txt`
**Max size:** 50 MB

---

### Search (`/api/search`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/search` | Direct semantic search against any Qdrant collection |

**Query params:** `q`, `collection` (code-embeddings / doc-embeddings / instruction-embeddings), `topK` (1–20), `repository`, `language`

---

### Conversations (`/api/conversations`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/conversations` | Paginated list of user's conversations (default 20). Returns ID, timestamps, message count, last message preview |
| GET | `/api/conversations/{id}` | Full conversation with all messages and metadata |

---

### Jobs (`/api/jobs`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/jobs` | Recent ingestion jobs (default 50) with summary counts by status and per-job progress |
| GET | `/api/jobs/{id}` | Single job status — type, status, processedFiles, totalFiles, timestamps, errorMessage |

---

### Admin (`/api/admin`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/admin/prompt-templates` | List all prompt templates |
| PUT | `/api/admin/prompt-templates/{taskType}` | Create or update a prompt template |
| GET | `/api/admin/feature-flags` | List all feature flags and their enabled status |
| PUT | `/api/admin/feature-flags/{name}` | Create or update a feature flag |
| GET | `/api/admin/audit-logs` | Recent audit log entries (default 100) |

---

### Config & Health

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/config` | Returns current ChatModel, EmbeddingModel, ChunkSize, TopK, Temperature, feature flags |
| GET | `/health` | Liveness probe — returns 200 OK |
| GET | `/health/ready` | Readiness probe — checks Ollama, Qdrant, PostgreSQL |

---

### OpenAI-Compatible (`/v1`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/v1/models` | List available models (ai-assistant, assistant-14b, assistant-30b) |
| GET | `/v1/models/{modelId}` | Single model info |
| POST | `/v1/chat/completions` | OpenAI Chat Completions format (blocking and streaming) |
| POST | `/v1/responses` | OpenAI Responses API format (GitHub Copilot requirement) |
| POST | `/v1/messages` | Anthropic Messages API format (GitHub Copilot requirement) |

---

## 2. Agent Pipeline

The pipeline uses a **registry-router-orchestrator** pattern. No file needs to change to add a new agent — register it in `Program.cs`.

```
User Request
     │
     ▼
OrchestratorAgent
     │── ClassifyIntent()
     │── IAgentRouter.RouteAsync() → ordered agent name list
     │
     ├── InstructionAgent   (retrieval — populates context.InstructionRules)
     ├── RepositoryAgent    (retrieval — populates context.RetrievedChunks)
     └── CodingAgent        (generation — returns response text)
```

### IAgentRegistry (Singleton)

Holds `AgentRegistration` records, each with:
- `Name` — unique identifier used by routers
- `Description` — shown to LLM router for selection
- `Condition` — predicate `(AgentContext) → bool` evaluated before each run
- `Factory` — `(IServiceProvider) → IAgent` creates the agent from DI

### AgentContext (per request)

Shared state flowing through the pipeline:

| Property | Set By | Read By |
|----------|--------|---------|
| `UserMessage` | Caller | All agents |
| `Intent` | OrchestratorAgent | RepositoryAgent, CodingAgent |
| `InstructionRules` | InstructionAgent | CodingAgent |
| `RetrievedChunks` | RepositoryAgent | CodingAgent |
| `MessagesOverride` | Caller (Continue.dev) | CodingAgent |
| `RepositoryFilter` | Caller | RepositoryAgent |
| Events | Each agent | Callers / logging |

---

### InstructionAgent

Retrieves relevant coding standards from `instruction-embeddings`.

**Dynamic tag matching flow:**
1. Embed the user query + intent hint
2. Fetch tag vocabulary from `TagVocabularyCache` (0 ms when warm)
3. Cosine similarity between query vector and each tag embedding
4. Select top tags above 0.35 similarity threshold (max 4)
5. Run parallel Qdrant searches filtered by matched `language`/`category` tags
6. Merge, deduplicate by content, trim to `InstructionTopK`
7. Falls back to unfiltered search if vocabulary is empty or no tags match

**Publishes:** `InstructionsRetrievedEvent` (rule count + matched tag list)

**Skipped when:** `instruction-embeddings` feature flag is disabled.

---

### RepositoryAgent

Retrieves relevant code chunks from `code-embeddings` and/or `doc-embeddings`.

**Flow:**
1. Check feature flags for enabled collections
2. Embed user message once
3. Search enabled collections in parallel
4. Apply optional `RepositoryFilter` to code-embeddings
5. Merge results into `context.RetrievedChunks`

**Publishes:** `ChunksRetrievedEvent` (chunk count + active collections)

**Non-fatal:** Returns empty chunks on failure, pipeline continues.

---

### CodingAgent

Generates the final response.

**Flow:**
1. Load prompt template from PostgreSQL by detected intent
2. Fill placeholders: `{instructions}`, `{context_chunks}`, `{user_message}`, `{language}`
3. Detect dominant language from retrieved chunks (defaults to C#)
4. Falls back to hardcoded system prompt if no template found
5. If `MessagesOverride` set (Continue.dev): skip template, pass messages directly to Ollama

**Supports:** Blocking (`ExecuteAsync`) and streaming (`StreamAsync` via `IStreamingAgent`)

**Publishes:** `ResponseGeneratedEvent` (latency + streamed flag)

---

## 3. Intent Classification

Keyword-based, evaluated in priority order:

| Intent | Trigger Keywords |
|--------|-----------------|
| `UnitTest` | unit test, write test, test for, xunit, nunit, moq |
| `CodeReview` | review, improve, refactor, clean up, optimise, optimize, fix |
| `CodeGeneration` | generate, create, write, implement, build, scaffold, add |
| `CodeExplanation` | explain, what does, how does, describe, what is, how is |
| `Documentation` | document, xml doc, summary comment, readme, docs for |
| `RepositoryQuestion` | repository, repo, codebase, file, class, method, namespace, project |
| `GeneralQuestion` | *(default fallback)* |

**RepositoryAgent is invoked** for: `CodeReview`, `RepositoryQuestion`, `CodeExplanation`, `CodeGeneration`

---

## 4. Routing System

Two router implementations selected via `Assistant:UseAiRouter` config flag.

### RulesAgentRouter (default, `UseAiRouter: false`)

Deterministic keyword-based selection:
- Always includes `InstructionAgent`
- Includes `RepositoryAgent` only if intent requires code context
- Always includes `CodingAgent`

### LlmAgentRouter (`UseAiRouter: true`)

Asks the LLM (configurable `RouterModel`, defaults to `ChatModel`):
- System prompt lists available agent names and descriptions
- Parses JSON array response: `["InstructionAgent", "CodingAgent"]`
- Validates names against registry — rejects unknown names
- Falls back to `RulesAgentRouter` on any error

**To use a faster/smaller routing model:** set `Assistant:RouterModel` to a smaller Ollama model.

---

## 5. Message Bus (Agent Events)

Each agent publishes a typed event to `AgentContext` after completing work. Events are stored in-process per request and can be read by any downstream consumer.

| Event | Published By | Payload |
|-------|-------------|---------|
| `IntentClassifiedEvent` | OrchestratorAgent | Intent, RouterUsed |
| `InstructionsRetrievedEvent` | InstructionAgent | RuleCount, MatchedTags |
| `ChunksRetrievedEvent` | RepositoryAgent | ChunkCount, Collections |
| `ResponseGeneratedEvent` | CodingAgent | LatencyMs, Streamed |

**API:** `context.PublishEvent(event)`, `context.GetEvents<T>()`, `context.AllEvents`

---

## 6. Ingestion & RAG Pipeline

### IngestionWorker (BackgroundService)

Runs inside `AssistantApi`. Polls PostgreSQL every 10 seconds. On startup, resets stuck `Running` jobs to `Queued` for crash recovery.

**Job types dispatched:**

| Type | Source | Target Collection |
|------|--------|-------------------|
| `GitRepository` | Clone/fetch via LibGit2Sharp | `code-embeddings` |
| `ZipUpload` | Extracted archive | `code-embeddings` |
| `Document` | PDF / DOCX / MD / TXT | `doc-embeddings` |
| `InstructionFile` | PDF / DOCX / MD / TXT | `instruction-embeddings` |

---

### File Parsing

**Supported source types:**

| Category | Extensions |
|----------|-----------|
| Source code | `.cs`, `.js`, `.ts`, `.tsx`, `.jsx`, `.py`, `.go`, `.java`, `.cpp`, `.c`, `.h`, `.rs`, `.rb`, `.php`, `.swift`, `.kt`, `.sql`, `.sh`, `.ps1` |
| Config | `.json`, `.xml`, `.yml`, `.yaml`, `.toml`, `.ini`, `.cfg`, `.config` |
| Markup | `.html`, `.css`, `.scss`, `.sass`, `.md`, `.markdown` |
| Plain text | `.txt`, `.env` |

**Parsers:**

| Parser | Handles | Notes |
|--------|---------|-------|
| `PlainTextParser` | Code, config, markup, text | Detects language from extension |
| `MarkdownParser` | `.md`, `.markdown` | Extracts YAML front matter, strips markdown syntax via Markdig |
| `PdfParser` | `.pdf` | Extracts per-page text via PdfPig |
| `DocxParser` | `.docx` | Extracts paragraphs via OpenXML |

**Excluded paths:** `node_modules`, `bin`, `obj`, `.git`, `__pycache__`, `dist`, `build`, `.next`, `vendor`, `packages`

---

### Chunking

- **Target size:** `ChunkSize` tokens (default 512), approximated as characters ÷ 4
- **Overlap:** `ChunkOverlap` tokens (default 64) for context continuity across boundaries
- **Split strategy:** splits at whitespace boundaries — no mid-word cuts
- Each chunk carries parsed metadata (language, doc_type, source)

---

### Deduplication

- **Algorithm:** SHA256 hash of entire file content
- **Storage:** `FileHash` table with `(repository_id, file_path, hash, chunk_count)`
- **Document scope:** `Guid.Empty` used as repository ID (documents are repo-agnostic)
- **Effect:** Skips re-parsing and re-embedding of unchanged files on re-index

---

### Embedding Pipeline

- **Batch size:** 32 chunks per Ollama `/api/embed` call
- **EmbedAndReturnAsync:** embeds and returns float[][] — used when vectors needed downstream (instruction enrichment)
- **EmbedAndUpsertAsync:** embeds and stores to Qdrant in one pass
- **Partial failure:** failed batches are skipped with a warning; remaining batches continue

---

### Instruction Document Enrichment

After embedding, each instruction chunk is categorized by the LLM:

1. LLM classifies chunk → `{ language, category, keywords, summary }` (JSON)
2. `DocumentTag` row persisted to PostgreSQL
3. Qdrant point re-upserted with enriched metadata (`language`, `category`, `keywords`)
4. `TagVocabularyCache` invalidated after all chunks processed

**TagVocabularyCache (singleton):**
- Pre-embeds all `(language, category)` pairs from `DocumentTags` table
- Cached for 5 minutes; rebuilt on next access after expiry or invalidation
- Used by `InstructionAgent` at query time for cosine similarity tag matching

**Graceful degradation:** If LLM categorization fails for a chunk, the chunk is still indexed in Qdrant — just without dynamic tags.

---

## 7. Vector Search

### Collections

| Name | Dimensions | Distance | Content |
|------|-----------|----------|---------|
| `code-embeddings` | 768 | Cosine | Source code chunks from repositories |
| `doc-embeddings` | 768 | Cosine | Uploaded PDFs, DOCX, Markdown, TXT |
| `instruction-embeddings` | 768 | Cosine | Coding standards and rules |

### Point Schema

Each Qdrant point stores:
- **ID:** deterministic UUID from SHA256 of composite key (repo + path + chunk index)
- **Vector:** 768-dim float array from `nomic-embed-text`
- **Content:** full chunk text
- **Metadata:** key-value pairs including `repository`, `file_path`, `language`, `branch`, `source`, `collection_type`, `category`, `keywords`

### Search Capabilities

- Cosine similarity search with configurable `topK`
- Metadata filters: `repository`, `language`, `category` (any combination)
- `DeleteByFilterAsync`: bulk delete by metadata filter (used during re-index and delete)
- `UpsertBatchAsync`: bulk insert/update

---

## 8. LLM Integration (Ollama)

### Endpoints Used

| Endpoint | Purpose | Called By |
|----------|---------|-----------|
| `POST /api/chat` | Blocking chat completion | CodingAgent, IngestionPipeline (categorization), LlmAgentRouter |
| `POST /api/chat` (streaming) | Token-by-token response | CodingAgent streaming |
| `POST /api/embed` | Batch text embedding (32 per call) | EmbeddingPipeline, InstructionAgent, RepositoryAgent |
| `GET /api/tags` | List available models (health check) | OllamaHealthCheck |

### Models

| Role | Default | Config Key |
|------|---------|-----------|
| Chat / generation | `qwen2.5-coder:7b` | `Assistant:ChatModel` |
| Embeddings | `nomic-embed-text` | `Assistant:EmbeddingModel` |
| Agent routing (optional) | *(same as ChatModel)* | `Assistant:RouterModel` |

### HTTP Client Resilience

- **Timeout per attempt:** 8 minutes (LLM inference can be slow)
- **Total timeout:** 10 minutes
- **Retries:** 2 (with 2-second delay)
- **Circuit breaker:** activates after failures within a 17-minute sampling window

---

## 9. Infrastructure Integrations

| Service | Technology | Purpose |
|---------|-----------|---------|
| PostgreSQL | EF Core + Npgsql | All relational data (jobs, conversations, templates, flags, audit) |
| Qdrant | gRPC client | Vector storage and semantic search |
| Ollama | HTTP client | LLM inference and text embedding |
| LibGit2Sharp | Native | Git repository clone and fetch |
| PdfPig | NuGet | PDF text extraction |
| DocumentFormat.OpenXml | NuGet | DOCX paragraph extraction |
| Markdig | NuGet | Markdown rendering and stripping |

---

## 10. Authentication & Authorization

### JWT Bearer Authentication

- Validates `Jwt:Secret` signing key (minimum 32 characters)
- Extracts user identity from `email`, `sub`, or `ClaimTypes.Email` claim → stored in `HttpContext.Items["UserId"]`
- Token lifetime validated; 5-minute clock skew tolerance
- **Unauthenticated requests:** 401 with JSON error body (not default redirect)

### API Key Bypass

- Static API key (`Jwt:Secret` value) accepted as `Bearer {key}` on all `/v1/*` routes
- Allows Open WebUI, Continue.dev, and other clients without full JWT flow
- Injects synthetic `ClaimsPrincipal` with `sub: api-key-client`

### Notes

- No role-based access control currently implemented
- Admin endpoints should add role checks before production use

---

## 11. Rate Limiting

Fixed-window rate limiter, 1-minute windows, no queuing (rejects immediately with 429).

| Policy | Limit | Applied To |
|--------|-------|-----------|
| `chat` | 20 req/min | POST `/api/chat`, `/api/chat/stream`, `/v1/chat/completions`, `/v1/responses`, `/v1/messages` |
| `ingestion` | 10 req/min | POST `/api/repositories`, `/api/repositories/upload`, `/api/documents`, `/api/documents/instructions` |

---

## 12. Health Checks

| Check | Endpoint | What It Tests |
|-------|----------|--------------|
| Liveness | `GET /health` | 200 OK (process alive) |
| Ollama | `GET /health/ready` | `GET /api/tags` returns 200 |
| Qdrant | `GET /health/ready` | gRPC health endpoint responds |
| PostgreSQL | `GET /health/ready` | `SELECT 1` succeeds |

**Response format:** JSON with overall status and per-check details.

---

## 13. Logging & Observability

### Serilog

- Structured JSON logging to Console
- Per-source log level overrides (Microsoft, EFCore, OpenTelemetry → Warning)
- Custom output template: `[HH:mm:ss LVL] SourceContext: Message`
- Request logging middleware: method, path, status, duration on every request

### OpenTelemetry

- **Tracing:** ASP.NET Core instrumentation, HttpClient instrumentation, `RecordException: true`
- **Metrics:** ASP.NET Core and HttpClient instrumentation
- **Service name:** `assistant-api`
- No console exporter (suppressed to avoid histogram noise)

### Agent Event System

Typed events published per request (see [Section 5](#5-message-bus-agent-events)) — available for logging, diagnostics, or future observability export.

---

## 14. Validation & Error Handling

### FluentValidation

| Validator | Guards |
|-----------|--------|
| `ChatRequestValidator` | Message not empty, ConversationId format |
| `RepositoryValidator` | URL format, branch name |
| `SearchRequestValidator` | Query not empty, topK 1–20, valid collection name |
| `FileUploadValidator` | Extension in allowed list, size ≤ 50 MB |

### ValidationExceptionMiddleware

- Catches `FluentValidation.ValidationException` → 400 with field-level grouped errors
- Catches unhandled exceptions → 500 with generic message (no stack trace to client)
- Registered as outermost middleware (wraps everything)

---

## 15. Audit Logging

Every request is logged to the `AuditLogs` table after the response is sent.

**Captured fields:** `TraceId`, `UserId`, `Method`, `Path`, `StatusCode`, `DurationMs`, `IpAddress`, `UserAgent`, `CreatedAt`

**Skipped paths:** `/health`, `/health/ready`, `/swagger`, `/favicon.ico`

**Implementation:** Fire-and-forget background task with its own DI scope (avoids DbContext conflicts with the main request scope).

---

## 16. Feature Flags

Runtime toggles stored in PostgreSQL. Evaluated per request. Manageable via `PUT /api/admin/feature-flags/{name}`.

| Flag | Default | Effect |
|------|---------|--------|
| `code-embeddings` | `false` | RepositoryAgent searches code-embeddings |
| `doc-embeddings` | `true` | RepositoryAgent searches doc-embeddings |
| `instruction-embeddings` | `true` | InstructionAgent runs |

Seeded automatically on first startup. Flip without redeployment.

---

## 17. Prompt Templates

Stored in PostgreSQL, seeded on first startup, editable via API.

**Placeholders:** `{instructions}`, `{context_chunks}`, `{user_message}`, `{language}`

| Task Type | Focus |
|-----------|-------|
| `CodeGeneration` | Production-ready code, follow standards, use codebase context |
| `CodeReview` | Correctness, standards compliance, improvement suggestions |
| `UnitTest` | AAA pattern, comprehensive coverage, mocking |
| `Documentation` | Purpose, parameters, examples, XML docs |
| `CodeExplanation` | Clear explanation, architecture references |
| `RepositoryQuestion` | Answer from codebase context, cite files |
| `GeneralQuestion` | General software engineering Q&A |

---

## 18. Startup Automation

`InitialiseAsync()` runs on every startup before the API begins serving requests:

1. **EF Core migrations** — `MigrateAsync()` applies any pending migrations idempotently
2. **Qdrant collections** — creates `code-embeddings`, `doc-embeddings`, `instruction-embeddings` if missing (768 dims, cosine distance)
3. **Seed feature flags** — inserts 3 default flags if not present
4. **Seed prompt templates** — inserts 7 default templates if not present
5. **IngestionWorker startup** — resets stuck `Running` jobs to `Queued`

**First-run guarantee:** Cloning the repo and running `docker compose up` produces a fully functional system with no manual DB or Qdrant setup.

---

## 19. Streaming Support

Three streaming protocols supported on `/v1/*` endpoints:

### OpenAI Chat Completions (SSE)
```
data: {"id":"","object":"chat.completion.chunk","created":1234,"model":"","choices":[{"index":0,"delta":{"content":"token"},"finish_reason":null}]}
data: [DONE]
```

### OpenAI Responses API (SSE)
Event types: `response.created`, `response.output_item.added`, `response.output_text.delta`, `response.completed`

### Anthropic Messages API (SSE)
Event types: `message_start`, `content_block_start`, `content_block_delta`, `content_block_stop`, `message_delta`, `message_stop`

### Native Chat Streaming
`POST /api/chat/stream` → `data: {"token":"..."}` per token, `data: [DONE]` at end

---

## 20. OpenAI-Compatible API

Compatible with any OpenAI client library. Tested integrations:

| Client | Connection Point | Notes |
|--------|-----------------|-------|
| **Open WebUI** | `/v1/chat/completions` | Manual connection via Admin Panel → Settings → Connections |
| **Continue.dev** | `/v1/chat/completions` | Detects `<tool_use_instructions>` in system message → passes messages directly to LLM |
| **GitHub Copilot** | `/v1/chat/completions`, `/v1/responses`, `/v1/messages` | Full tool-calling two-turn protocol |
| **Any OpenAI SDK** | `/v1/models`, `/v1/chat/completions` | Standard OpenAI format |

---

## 21. VS Code Extension & Tool Calling

### Tool Calling Protocol

`ToolCallService` handles the two-turn GitHub Copilot / VS Code tool-calling flow:

**Turn 1 — Generation:**
1. Augments user prompt with file-operation instructions
2. LLM responds with `### File: <path>` blocks
3. Parser extracts file path + content from each block
4. Returns OpenAI `tool_calls[]` with `finish_reason: "tool_calls"`

**Turn 2 — Confirmation:**
1. Client sends `role: "tool"` messages with file operation results
2. Service returns plain confirmation message

**Tool schemas synthesized:**
- `create_new_file` — creates a new file at the specified path
- `edit_existing_file` — overwrites an existing file with new content

**Note:** The API never writes to disk. The VS Code extension executes tool calls via `vscode.workspace.fs`. In a plain chat call, the `### File:` blocks are returned as raw text.

---

## 22. Configuration Reference

```json
{
  "Assistant": {
    "ChatModel": "qwen2.5-coder:7b",
    "EmbeddingModel": "nomic-embed-text",
    "ChunkSize": 512,
    "ChunkOverlap": 64,
    "TopK": 3,
    "InstructionTopK": 2,
    "Temperature": 0.2,
    "VectorSize": 768,
    "UseAiRouter": false,
    "RouterModel": ""
  },
  "ConnectionStrings": {
    "Postgres": "Host=...;Port=5432;Database=assistant_db;Username=assistant;Password=..."
  },
  "Ollama": { "BaseUrl": "http://ollama:11434" },
  "Qdrant": { "Host": "qdrant", "Port": "6334" },
  "Jwt": { "Secret": "<32+ char secret>" },
  "Ingestion": {
    "RepositoryCachePath": "/data/repository-cache",
    "UploadPath": "/data/uploads"
  }
}
```

| Key | Default | Effect |
|-----|---------|--------|
| `UseAiRouter` | `false` | `true` → LlmAgentRouter; `false` → RulesAgentRouter |
| `RouterModel` | `""` | Ollama model for routing; empty = use ChatModel |
| `TopK` | `3` | Max code/doc chunks per request |
| `InstructionTopK` | `2` | Max instruction rules per request |
| `VectorSize` | `768` | Must match embedding model (nomic-embed-text=768, mxbai-embed-large=1024) |

---

## 23. Docker & Deployment

### Services

| Container | Image | Port | Purpose |
|-----------|-------|------|---------|
| `assistant-api` | Custom .NET build | 5000 | Main API + IngestionWorker |
| `ollama` | `ollama/ollama:latest` | 11434 | LLM inference and embeddings |
| `qdrant` | `qdrant/qdrant:latest` | 6333, 6334 | Vector database |
| `postgres` | `postgres:16-alpine` | 5432 | Relational database |
| `open-webui` | `ghcr.io/open-webui/open-webui:main` | 3000 | Web chat interface |

### Volumes (external, pre-created)

`ollama-models`, `qdrant-data`, `postgres-data`, `openwebui-data`, `repository-cache`, `uploads-data`

### Quick Start

```powershell
# 1. Create volumes and network
docker volume create ollama-models qdrant-data postgres-data openwebui-data repository-cache uploads-data
docker network create ai-assistant-net

# 2. Start infrastructure
docker compose up -d

# 3. Pull models
docker exec ollama ollama pull nomic-embed-text
docker exec ollama ollama pull qwen2.5-coder:7b

# 4. Build and start API
docker compose build assistant-api
docker compose up -d assistant-api

# 5. Verify
curl http://localhost:5000/health/ready
```

No manual database setup, no manual Qdrant collection creation — all handled on first startup.

---

## 24. Data Models

### Entities

| Entity | Key Fields |
|--------|-----------|
| `Repository` | URL, Branch, Status, FileCount, ChunkCount, LocalPath, PAT, ErrorMessage |
| `IngestionJob` | Type, Status, SourcePath, OriginalFileName, ProcessedFiles, TotalFiles, timestamps, ErrorMessage |
| `FileHash` | RepositoryId, FilePath, SHA256Hash, ChunkCount |
| `Conversation` | UserId, CreatedAt, UpdatedAt |
| `ConversationMessage` | ConversationId, Role, Content, Intent, LatencyMs |
| `PromptTemplate` | TaskType, SystemPrompt, UserPromptTemplate, IsActive |
| `FeatureFlag` | Name, IsEnabled, Description |
| `AuditLog` | TraceId, UserId, Method, Path, StatusCode, DurationMs, IpAddress, UserAgent |
| `DocumentTag` | PointId, SourceFile, Language, Category, Keywords, Summary, CreatedAt |

### Enums

| Enum | Values |
|------|--------|
| `AgentIntent` | CodeGeneration, CodeExplanation, CodeReview, UnitTest, Documentation, RepositoryQuestion, GeneralQuestion |
| `IndexingStatus` | Pending, Indexing, Completed, Failed |
| `IngestionJobType` | GitRepository, ZipUpload, Document, InstructionFile |
| `IngestionJobStatus` | Queued, Running, Completed, Failed |
| `DocumentCollection` | Documents, Instructions |

---

## 25. Architecture Patterns

| Pattern | Where Used |
|---------|-----------|
| **Clean Architecture** | Core → Application → Infrastructure → API (dependencies point inward) |
| **Repository Pattern** | `IMetadataRepository`, `IVectorRepository`, `IFileHashRepository`, `IDocumentTagRepository` |
| **Registry + Factory** | `IAgentRegistry` stores factories; agents resolved on demand |
| **Strategy** | `IDocumentParser` per file type, `IAgentRouter` (rules vs. LLM), `IChunkingService` |
| **Pipeline / Chain** | `OrchestratorAgent` runs agents in sequence; each modifies shared context |
| **Event Bus (in-process)** | `AgentContext.PublishEvent` / `GetEvents<T>` — typed events per pipeline run |
| **Background Service** | `IngestionWorker : BackgroundService` hosted in the web API process |
| **Options Pattern** | `AssistantOptions`, `IngestionOptions` via `IOptions<T>` |
| **Fire-and-Forget** | `AuditMiddleware` writes to DB after response without blocking |

---

## 26. Performance Optimizations

| Optimization | Detail |
|-------------|--------|
| **Embedding batching** | 32 chunks per Ollama `/api/embed` call (32× fewer HTTP round trips) |
| **Parallel file parsing** | Up to 2× CPU core count concurrent file reads |
| **Parallel vector search** | Multiple Qdrant searches run concurrently (`Task.WhenAll`) |
| **Tag vocabulary cache** | 5-minute in-memory cache of pre-embedded instruction tags — 0 ms on cache hit |
| **Content deduplication** | Skip re-embedding unchanged files (SHA256 hash check) |
| **Streaming** | Token-by-token response delivery — no full-response buffering |
| **Deterministic point IDs** | SHA256-derived UUIDs enable idempotent upserts (no duplicate vectors on re-index) |

---

## 27. Resilience & Graceful Degradation

| Scenario | Behaviour |
|----------|-----------|
| Ollama call fails | Retry 2× with 2s delay; circuit breaker after repeated failures |
| Embedding batch fails | Skip batch, log warning, continue with remaining chunks |
| LLM categorization fails | Chunk indexed without tags; pipeline continues |
| InstructionAgent fails | Pipeline continues with empty instruction rules |
| RepositoryAgent fails | Pipeline continues with empty retrieved chunks |
| No prompt template in DB | Falls back to hardcoded system prompt |
| LlmAgentRouter returns invalid names | Falls back to RulesAgentRouter |
| LlmAgentRouter throws | Falls back to RulesAgentRouter |
| Job running when process crashed | Reset to Queued on next startup |
| Qdrant collection missing | Auto-created on startup |
| DB tables missing | EF Core migrations applied on startup |

---

## 28. What Is NOT Implemented

| Feature | Notes |
|---------|-------|
| **Exact file read** | LLM can only retrieve nearest-match chunks, not read a specific file verbatim |
| **File listing** | No "list all files in repo" capability for the LLM |
| **Run tests** | No shell execution, no `dotnet test` runner |
| **Modify files (server-side)** | API suggests edits via tool calls; the VS Code extension applies them — server never writes to disk |
| **Multi-tenant isolation** | No per-customer data separation (TenantId) — all users share collections and DB rows |
| **Role-based access control** | No admin/user role distinction on API endpoints |
| **MCP tool integration** | Phase 7 — planned, not built |
| **GPU acceleration** | Docker Compose config present but commented out |
| **Conversation context injection** | Prior messages not injected into LLM prompt (stateless per request) |
