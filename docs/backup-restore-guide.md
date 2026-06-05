# Backup and Restore Guide

This guide covers backup and restore procedures for all stateful components:
PostgreSQL, Qdrant, Ollama models, and Open WebUI data.

---

## Overview

| Component | What it stores | Backup method | Restore method |
|---|---|---|---|
| PostgreSQL | Repositories, jobs, conversations, audit logs, feature flags, prompt templates | `pg_dump` | `pg_restore` |
| Qdrant | All vector embeddings (code, documents, instructions) | Qdrant snapshot API | Qdrant restore API |
| Ollama | Downloaded model weights | Volume backup | Re-pull or volume restore |
| Open WebUI | Users, chat history, settings | Volume backup | Volume restore |

---

## PostgreSQL

### Backup

Dump the full database to a `.dump` file (custom format, compressed):

```powershell
# Create a backup directory on the host
New-Item -ItemType Directory -Force backups/postgres

# Dump the database
docker exec postgres pg_dump `
  -U assistant `
  -d assistant_db `
  -F custom `
  -f /tmp/assistant_db_backup.dump

# Copy the dump out of the container
docker cp postgres:/tmp/assistant_db_backup.dump backups/postgres/assistant_db_$(Get-Date -Format "yyyyMMdd_HHmmss").dump
```

### Restore

```powershell
# Copy the backup file into the container
docker cp backups/postgres/assistant_db_20240101_120000.dump postgres:/tmp/restore.dump

# Drop and recreate the database (warning: destroys all current data)
docker exec postgres psql -U assistant -c "DROP DATABASE IF EXISTS assistant_db;"
docker exec postgres psql -U assistant -c "CREATE DATABASE assistant_db OWNER assistant;"

# Restore from dump
docker exec postgres pg_restore `
  -U assistant `
  -d assistant_db `
  --no-owner `
  --role=assistant `
  /tmp/restore.dump
```

### Verify restore

```powershell
docker exec postgres psql -U assistant -d assistant_db -c 'SELECT COUNT(*) FROM "Repositories";'
docker exec postgres psql -U assistant -d assistant_db -c 'SELECT COUNT(*) FROM "PromptTemplates";'
```

---

## Qdrant

Qdrant snapshots capture all collections and their vectors.

### Backup — create snapshots

Create a snapshot for each collection:

```powershell
New-Item -ItemType Directory -Force backups/qdrant

# Snapshot code-embeddings
$snap1 = (curl -s -X POST http://localhost:6333/collections/code-embeddings/snapshots | ConvertFrom-Json).result.name
Write-Host "code-embeddings snapshot: $snap1"

# Snapshot doc-embeddings
$snap2 = (curl -s -X POST http://localhost:6333/collections/doc-embeddings/snapshots | ConvertFrom-Json).result.name
Write-Host "doc-embeddings snapshot: $snap2"

# Snapshot instruction-embeddings
$snap3 = (curl -s -X POST http://localhost:6333/collections/instruction-embeddings/snapshots | ConvertFrom-Json).result.name
Write-Host "instruction-embeddings snapshot: $snap3"
```

### Download snapshots to host

```powershell
# List available snapshots
curl http://localhost:6333/collections/code-embeddings/snapshots
curl http://localhost:6333/collections/doc-embeddings/snapshots
curl http://localhost:6333/collections/instruction-embeddings/snapshots

# Download each snapshot (replace <snapshot-name> with the name returned above)
curl -o backups/qdrant/code-embeddings_<snapshot-name>.snapshot `
  http://localhost:6333/collections/code-embeddings/snapshots/<snapshot-name>

curl -o backups/qdrant/doc-embeddings_<snapshot-name>.snapshot `
  http://localhost:6333/collections/doc-embeddings/snapshots/<snapshot-name>

curl -o backups/qdrant/instruction-embeddings_<snapshot-name>.snapshot `
  http://localhost:6333/collections/instruction-embeddings/snapshots/<snapshot-name>
```

### Restore

Upload a snapshot to restore a collection. If the collection already exists, delete it first:

```powershell
# Delete existing collection (if it exists)
curl -X DELETE http://localhost:6333/collections/code-embeddings

# Restore from snapshot file
curl -X POST "http://localhost:6333/collections/code-embeddings/snapshots/upload?priority=snapshot" `
  -H "Content-Type: multipart/form-data" `
  -F "snapshot=@backups/qdrant/code-embeddings_<snapshot-name>.snapshot"
```

Repeat for `doc-embeddings` and `instruction-embeddings`.

### Verify restore

```powershell
# Check collection info (should show vector count)
curl http://localhost:6333/collections/code-embeddings
curl http://localhost:6333/collections/doc-embeddings
curl http://localhost:6333/collections/instruction-embeddings
```

---

## Ollama Models

Model weights are stored in the `ollama-models` Docker volume.

### Option A — Volume backup (preserves all models)

```powershell
New-Item -ItemType Directory -Force backups/ollama

# Create a tar archive of the volume
docker run --rm `
  -v ollama-models:/data `
  -v ${PWD}/backups/ollama:/backup `
  alpine tar czf /backup/ollama_models_$(Get-Date -Format "yyyyMMdd").tar.gz -C /data .
```

### Option B — Re-pull (simpler, requires internet)

If the volume is lost, simply re-pull the models:

```powershell
docker exec ollama ollama pull qwen3-coder:30b    # or :14b or :7b
docker exec ollama ollama pull nomic-embed-text
```

### Restore from volume backup

```powershell
# Restore the volume from archive
docker run --rm `
  -v ollama-models:/data `
  -v ${PWD}/backups/ollama:/backup `
  alpine tar xzf /backup/ollama_models_20240101.tar.gz -C /data
```

---

## Open WebUI

Open WebUI stores users, chat history, and settings in the `openwebui-data` volume.

### Backup

```powershell
New-Item -ItemType Directory -Force backups/openwebui

docker run --rm `
  -v openwebui-data:/data `
  -v ${PWD}/backups/openwebui:/backup `
  alpine tar czf /backup/openwebui_$(Get-Date -Format "yyyyMMdd_HHmmss").tar.gz -C /data .
```

### Restore

```powershell
# Stop Open WebUI before restoring
docker compose stop open-webui

# Restore the volume
docker run --rm `
  -v openwebui-data:/data `
  -v ${PWD}/backups/openwebui:/backup `
  alpine sh -c "rm -rf /data/* && tar xzf /backup/openwebui_20240101_120000.tar.gz -C /data"

# Restart
docker compose start open-webui
```

---

## Full system backup script

Save as `scripts/backup.ps1` and run on a schedule:

```powershell
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupRoot = "backups/$timestamp"

New-Item -ItemType Directory -Force "$backupRoot/postgres"
New-Item -ItemType Directory -Force "$backupRoot/qdrant"
New-Item -ItemType Directory -Force "$backupRoot/openwebui"

Write-Host "Backing up PostgreSQL..."
docker exec postgres pg_dump -U assistant -d assistant_db -F custom -f /tmp/backup.dump
docker cp postgres:/tmp/backup.dump "$backupRoot/postgres/assistant_db.dump"

Write-Host "Backing up Qdrant snapshots..."
foreach ($col in @("code-embeddings", "doc-embeddings", "instruction-embeddings")) {
    $snapName = (Invoke-RestMethod -Method Post "http://localhost:6333/collections/$col/snapshots").result.name
    Invoke-WebRequest "http://localhost:6333/collections/$col/snapshots/$snapName" `
        -OutFile "$backupRoot/qdrant/${col}.snapshot"
}

Write-Host "Backing up Open WebUI..."
docker run --rm `
  -v openwebui-data:/data `
  -v ${PWD}/$backupRoot/openwebui:/backup `
  alpine tar czf /backup/openwebui.tar.gz -C /data .

Write-Host "Backup complete: $backupRoot"
```

---

## Backup retention

Keep at least:
- Daily backups for 7 days
- Weekly backups for 4 weeks
- Monthly backups for 3 months

Clean old backups (keep last 7 days):

```powershell
Get-ChildItem backups -Directory |
  Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-7) } |
  Remove-Item -Recurse -Force
```
