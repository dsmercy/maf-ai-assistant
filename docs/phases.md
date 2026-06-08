# Enterprise Local AI Coding Assistant — Build Phases

## PHASE 1 — FOUNDATION ✅
**Goal:** Project scaffold, Docker Compose, core infrastructure

- .NET 10 solution with Clean Architecture (Domain, Application, Infrastructure, API)
- Docker Compose: Ollama, Qdrant, PostgreSQL, Open WebUI
- EF Core with PostgreSQL — entities: Repository, IngestionJob, FileHash, PromptTemplate, FeatureFlag, AuditLog, Conversation, ConversationMessage
- Ollama client (chat + streaming + embeddings)
- Qdrant vector repository (upsert, search, delete)
- Health checks: Ollama, Qdrant, PostgreSQL
- Serilog structured logging + OpenTelemetry tracing

---

## PHASE 2 — INGESTION PIPELINE ✅
**Goal:** Index repositories and documents into Qdrant

- IngestionService background worker (polls PostgreSQL every 10s)
- Git repository cloning/fetching via LibGit2Sharp
- ZIP upload ingestion
- Document parsers: PDF, DOCX, Markdown, PlainText
- File change detection via SHA-256 content hashing (skip unchanged files)
- Duplicate upload detection for instruction/document files
- Three Qdrant collections: `code-embeddings`, `doc-embeddings`, `instruction-embeddings`
- Chunking service: configurable ChunkSize=512, ChunkOverlap=64
- REST endpoints: POST /api/repositories, POST /api/documents, POST /api/instructions

---

## PHASE 3 — RAG AGENT PIPELINE ✅
**Goal:** Semantic search + LLM prompt assembly

- OrchestratorAgent → InstructionAgent + RepositoryAgent → CodingAgent
- Intent classification: CodeGeneration, CodeReview, UnitTest, Documentation, CodeExplanation, RepositoryQuestion, GeneralQuestion
- InstructionAgent: parallel Qdrant search (general tags + language tag), TopK budget split
- RepositoryAgent: feature-flag controlled (code-embeddings, doc-embeddings)
- CodingAgent: loads PromptTemplate from PostgreSQL, fills placeholders, calls Ollama
- Prompt templates seeded on startup for all 7 intents
- Feature flags seeded on startup: code-embeddings, doc-embeddings, streaming, rag, auth, audit, rate-limit

---

## PHASE 4 — CHAT API + STREAMING ✅
**Goal:** REST chat endpoints + token streaming

- POST /api/chat — blocking response
- POST /api/chat/stream — Server-Sent Events token streaming
- POST /v1/chat/completions — OpenAI-compatible endpoint for Open WebUI
- Conversation persistence: messages stored in PostgreSQL
- Rate limiting: 20 req/min (chat), 10 req/min (ingestion)
- JWT authentication scaffold (disabled by default, toggled via feature flag)

---

## PHASE 5 — ADMIN + OBSERVABILITY ✅
**Goal:** Runtime management endpoints + audit trail

- GET/PUT /api/admin/feature-flags — toggle features at runtime without redeployment
- GET/PUT /api/admin/prompt-templates — customise LLM prompts per intent at runtime
- GET /api/admin/audit-logs — request audit trail
- GET /api/jobs, GET /api/jobs/{id} — ingestion job monitoring
- GET /health (liveness), GET /health/ready (readiness)
- AuditMiddleware: logs all requests to PostgreSQL

---

## PHASE 6 — INSTRUCTION FILE TAXONOMY + MODELFILE ✅
**Goal:** Structured coding standards injection + model customisation

- 35 topic-focused instruction files across 5 stacks (general, csharp, javascript, typescript, python)
- YAML front matter: language tag + category tag per file
- Language tag map: 35+ keyword → tag mappings in InstructionAgent
- Intent → general tag map: scopes retrieval to intent-relevant categories
- Deduplicated cross-stack rules (security, observability, testing, API, database, dependencies each owned by one file)
- Ollama Modelfile: general instructions baked into model system prompt (Modelfile.14b, Modelfile.30b)
- General tags search commented out in InstructionAgent — only language-specific rules retrieved per request
- Environment separation: Development (assistant_db_dev, local paths) vs Production (assistant_db, Docker paths)
- Custom model names: assistant-14b, assistant-30b

---

## PHASE 7 — CONTINUE.DEV VS CODE EXTENSION 🔲
**Goal:** Integrate the AI coding assistant directly into VS Code via Continue.dev

### What Continue.dev provides
- Inline code completions inside VS Code
- Chat sidebar (ask questions about open files, selected code)
- `/edit` command — apply AI suggestions directly to files
- `/explain`, `/test`, `/review` slash commands
- Context awareness: open files, selected code, terminal output, codebase indexing

### Integration approach
Continue.dev supports custom OpenAI-compatible providers. Our API already exposes `POST /v1/chat/completions` — wire it as a Continue.dev provider.

### Deliverables
- `continue-config/config.json` — Continue.dev configuration pointing to our API
- Configure model provider: `assistant-30b` (production) / `assistant-14b` (local dev)
- Configure context providers: codebase, open files, terminal, docs
- Configure slash commands: /review, /test, /explain mapped to our agent intents
- Document setup steps for developers: install extension, copy config, set API key
- Optional: custom system prompt override per slash command to match our intent classification

### Config location
`~/.continue/config.json` (per developer) or shared via `.continue/config.json` in repo root

---

## PHASE 8 — MCP TOOL INTEGRATION 🔲
**Goal:** Expose agent capabilities as MCP tools for Open WebUI and other MCP clients

### What MCP provides
- Standardised tool/function calling protocol
- Open WebUI can call our tools natively
- LLM decides when to invoke tools based on user intent

### Planned tools
- `search_codebase` — semantic search over code-embeddings
- `search_docs` — semantic search over doc-embeddings
- `search_instructions` — retrieve coding standards by language/intent
- `get_repository_info` — list indexed repositories and their status
- `ingest_document` — trigger document ingestion job
- `get_job_status` — check ingestion job progress

### Deliverables
- MCP server implementation (ASP.NET Core or standalone)
- Tool definitions with input/output schemas
- Registration in Open WebUI Admin Panel
- Authentication via shared secret
