# Deployment Guide

Enterprise Local AI Coding Assistant — end-to-end deployment on a single Docker host.

---

## Prerequisites

| Requirement | Minimum | Recommended |
|---|---|---|
| Docker Desktop / Docker Engine | 24.x | 26.x |
| Docker Compose | v2.x (plugin) | latest |
| RAM | 16 GB | 32 GB |
| Disk (free) | 40 GB | 80 GB |
| CPU | 8 cores | 16 cores |
| GPU (optional) | — | NVIDIA with 8 GB+ VRAM |

> **GPU support**: requires [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html).
> Uncomment the `deploy` section in `docker-compose.yml` under the `ollama` service to enable it.

---

## Step 1 — Clone the repository

```powershell
git clone <your-repo-url> ai-coding-assistant
cd ai-coding-assistant
```

---

## Step 2 — Configure environment variables

Copy the template and edit secrets:

```powershell
Copy-Item .env.example .env   # if .env.example exists, otherwise edit .env directly
```

Open `.env` and set the following values:

```dotenv
# PostgreSQL
POSTGRES_DB=assistant_db
POSTGRES_USER=assistant
POSTGRES_PASSWORD=<strong-password>        # CHANGE THIS

# Ollama
OLLAMA_HOST=0.0.0.0

# Qdrant
QDRANT_HOST=qdrant
QDRANT_PORT=6333

# API
ASPNETCORE_ENVIRONMENT=Production
JWT_SECRET=<random-32-char-string>         # CHANGE THIS — min 32 characters

# Open WebUI
WEBUI_SECRET_KEY=<random-32-char-string>   # CHANGE THIS — min 32 characters
OLLAMA_BASE_URL=http://ollama:11434
```

Generate secure random secrets (PowerShell):

```powershell
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
```

---

## Step 3 — Choose a chat model

Three model sizes are available. Choose based on your hardware:

| Model | VRAM / RAM | Speed | Quality | Best for |
|---|---|---|---|---|
| `qwen3-coder:7b` | ~8 GB RAM / 6 GB VRAM | Fast (~5s) | Good | Development machines, laptops |
| `qwen3-coder:14b` | ~16 GB RAM / 10 GB VRAM | Medium (~10s) | Better | Workstations, small servers |
| `qwen3-coder:30b` | ~32 GB RAM / 20 GB VRAM | Slow (~20s) | Best | Dedicated AI servers with GPU |

Edit `appsettings.json` (or set via environment variable) to select your model:

```json
"Assistant": {
  "ChatModel": "qwen3-coder:14b"
}
```

Or set it as a Docker environment variable in `docker-compose.yml` under `assistant-api`:

```yaml
environment:
  Assistant__ChatModel: "qwen3-coder:14b"
```

---

## Step 4 — Create Docker volumes and network

Run once on a fresh host:

```powershell
docker volume create ollama-models
docker volume create qdrant-data
docker volume create postgres-data
docker volume create openwebui-data
docker volume create repository-cache
docker network create ai-assistant-net
```

---

## Step 5 — Start infrastructure services

```powershell
docker compose up -d postgres qdrant ollama
```

Wait for all three to be healthy:

```powershell
docker compose ps
```

All three should show `healthy` before continuing.

---

## Step 6 — Pull the Ollama models

Pull the chat model you selected in Step 3 and the embedding model:

```powershell
# Pull the chat model (choose one):
docker exec ollama ollama pull qwen3-coder:7b
# docker exec ollama ollama pull qwen3-coder:14b
# docker exec ollama ollama pull qwen3-coder:30b

# Always pull the embedding model:
docker exec ollama ollama pull nomic-embed-text
```

Verify both are available:

```powershell
docker exec ollama ollama list
```

---

## Step 7 — Build and start the application services

```powershell
docker compose build assistant-api
docker compose up -d assistant-api open-webui
```

---

## Step 8 — Seed the database

Seed prompt templates (required for the agent pipeline):

```powershell
Get-Content schema/seed_prompt_templates.sql | docker exec -i postgres psql -U assistant -d assistant_db
```

Seed feature flags (optional defaults):

```powershell
Get-Content schema/seed_feature_flags.sql | docker exec -i postgres psql -U assistant -d assistant_db
```

---

## Step 9 — Verify all services

```powershell
# Health check
curl http://localhost:5000/health
curl http://localhost:5000/health/ready

# API is running
curl http://localhost:5000/api/config

# Qdrant is running
curl http://localhost:6333/healthz

# Ollama is running
curl http://localhost:11434/api/tags
```

All endpoints should return 200.

Open WebUI is available at: **http://localhost:3000**

---

## Step 10 — Connect Open WebUI to the assistant API

1. Open **http://localhost:3000** and create an admin account.
2. Go to **Admin Panel → Settings → Connections**.
3. Under **OpenAI API**, add a new connection:
   - **URL**: `http://assistant-api:5000/v1`
   - **API Key**: the value of `WEBUI_SECRET_KEY` from your `.env`
4. Save and refresh. The model `ai-assistant` will appear in the model selector.

---

## Switching models after deployment

To switch from one model to another:

1. Pull the new model:
   ```powershell
   docker exec ollama ollama pull qwen3-coder:14b
   ```

2. Update `docker-compose.yml` or `appsettings.json`:
   ```yaml
   Assistant__ChatModel: "qwen3-coder:14b"
   ```

3. Rebuild and restart the API:
   ```powershell
   docker compose up -d --force-recreate assistant-api
   ```

---

## Environment variable reference

| Variable | Description | Default |
|---|---|---|
| `POSTGRES_DB` | Database name | `assistant_db` |
| `POSTGRES_USER` | Database user | `assistant` |
| `POSTGRES_PASSWORD` | Database password | **must change** |
| `OLLAMA_HOST` | Ollama bind address | `0.0.0.0` |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` | `Development` |
| `JWT_SECRET` | JWT signing secret (min 32 chars) | **must change** |
| `WEBUI_SECRET_KEY` | Open WebUI session secret | **must change** |
| `OLLAMA_BASE_URL` | Ollama URL seen by Open WebUI | `http://ollama:11434` |

---

## Port reference

| Service | Host Port | Purpose |
|---|---|---|
| Open WebUI | 3000 | Chat UI |
| Ollama | 11434 | LLM API |
| Qdrant REST | 6333 | Vector search REST |
| Qdrant gRPC | 6334 | Vector search gRPC (used by API) |
| PostgreSQL | 5432 | Metadata database |
| Assistant API | 5000 | REST API |

---

## Stopping and removing

Stop all containers (data preserved):

```powershell
docker compose down
```

Stop and remove all data volumes:

```powershell
docker compose down -v
```
