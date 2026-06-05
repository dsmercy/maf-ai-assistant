# Local Development Guide

Run the ASP.NET Core API outside Docker for hot-reload and fast iteration while keeping
all backing services (Postgres, Qdrant, Ollama) running in Docker.

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0 |
| Docker Desktop | 24+ |
| Visual Studio 2022 / VS Code / Rider | any recent |

---

## Step 1 — Start backing services only

```powershell
docker compose up -d postgres qdrant ollama
```

Do **not** start `assistant-api` or `ingestion-service` — you will run those locally.

---

## Step 2 — Configure local connection strings

The containers expose ports to localhost. Create `AssistantApi/appsettings.Development.json`
to override connection strings for local development:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=assistant_db;Username=assistant;Password=change_this_password_in_production"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": "6334"
  },
  "Ingestion": {
    "RepositoryCachePath": "C:/data/repository-cache",
    "UploadPath": "C:/data/uploads"
  }
}
```

> `appsettings.Development.json` is excluded from git via `.gitignore`. Never commit local overrides.

Create the local data directories:

```powershell
New-Item -ItemType Directory -Force "C:/data/repository-cache"
New-Item -ItemType Directory -Force "C:/data/uploads"
```

---

## Step 3 — Restore and build

```powershell
dotnet restore AssistantSolution.sln
dotnet build AssistantSolution.sln
```

---

## Step 4 — Run the API with hot reload

```powershell
cd AssistantApi
dotnet watch run
```

The API starts at **http://localhost:5000**. Changes to `.cs` files trigger a live reload.

To run without watch (faster startup):

```powershell
dotnet run --project AssistantApi/AssistantApi.csproj
```

---

## Step 5 — Run the ingestion service locally (optional)

Open a second terminal:

```powershell
cd IngestionService
dotnet watch run
```

The ingestion worker polls the database every 10 seconds for queued jobs.

---

## Step 6 — Seed the database (first time only)

```powershell
Get-Content schema/seed_prompt_templates.sql | docker exec -i postgres psql -U assistant -d assistant_db
Get-Content schema/seed_feature_flags.sql | docker exec -i postgres psql -U assistant -d assistant_db
```

---

## Choosing a model for local development

Smaller models start faster and use less RAM when developing locally:

```json
// AssistantApi/appsettings.Development.json
{
  "Assistant": {
    "ChatModel": "qwen3-coder:7b",
    "Temperature": 0.2,
    "TopK": 3
  }
}
```

Pull the smaller model once:

```powershell
docker exec ollama ollama pull qwen3-coder:7b
```

---

## Swagger UI

When running with `ASPNETCORE_ENVIRONMENT=Development`, Swagger is available at:

**http://localhost:5000/swagger**

Use it to explore and test all API endpoints without a separate HTTP client.

---

## Useful development commands

```powershell
# List all registered repositories
curl http://localhost:5000/api/repositories

# Register a public repository for indexing
curl -X POST http://localhost:5000/api/repositories `
  -H "Content-Type: application/json" `
  -d '{"url":"https://github.com/owner/repo","branch":"main"}'

# Send a chat message
curl -X POST http://localhost:5000/api/chat `
  -H "Content-Type: application/json" `
  -d '{"message":"How does dependency injection work in this codebase?"}'

# Check running ingestion jobs
curl http://localhost:5000/api/jobs

# Search the vector index directly
curl "http://localhost:5000/api/search?q=repository+pattern&collection=code-embeddings&topK=5"

# View recent audit logs
curl http://localhost:5000/api/admin/audit-logs

# Check Qdrant collections
curl http://localhost:6333/collections

# Check which Ollama models are loaded
curl http://localhost:11434/api/tags
```

---

## Database inspection

Connect to PostgreSQL with any Postgres client (TablePlus, DBeaver, psql):

```
Host:     localhost
Port:     5432
Database: assistant_db
User:     assistant
Password: change_this_password_in_production
```

Or use psql directly:

```powershell
docker exec -it postgres psql -U assistant -d assistant_db
```

Useful queries:

```sql
-- All registered repositories
SELECT id, name, url, branch, status, file_count, last_indexed_at FROM "Repositories";

-- All ingestion jobs
SELECT id, repository_id, job_type, status, created_at, completed_at, error_message FROM "IngestionJobs";

-- Recent audit log entries
SELECT created_at, user_id, method, path, status_code, duration_ms FROM "AuditLogs" ORDER BY created_at DESC LIMIT 20;

-- Feature flags
SELECT name, is_enabled FROM "FeatureFlags";

-- Prompt templates
SELECT "TaskType", LEFT("SystemPrompt", 80) FROM "PromptTemplates";
```

---

## Running tests

```powershell
dotnet test AssistantSolution.sln
```

---

## Troubleshooting

**API fails to start with `Connection refused` on Postgres**
Ensure `docker compose up -d postgres` completed and the container is healthy:
```powershell
docker compose ps postgres
```

**Ollama returns 404 on `/api/embed`**
Pull `nomic-embed-text` if not already done:
```powershell
docker exec ollama ollama pull nomic-embed-text
```

**`Unable to parse UUID` error in ingestion logs**
This was a known issue fixed in the `MakePointId()` method in `IngestionPipeline.cs`. Ensure
you are on the latest code and rebuild.

**`OptionsValidationException: SamplingDuration` on startup**
Circuit breaker `SamplingDuration` must be at least twice the `AttemptTimeout`.
Current values in `DependencyInjection.cs`: AttemptTimeout=8min, SamplingDuration=17min.
Do not reduce `SamplingDuration` below 16 minutes.

**Hot reload not reflecting changes**
Some changes (new services registered in DI, `Program.cs` edits) require a full restart.
Press `Ctrl+R` in the `dotnet watch` console to force a full restart.
