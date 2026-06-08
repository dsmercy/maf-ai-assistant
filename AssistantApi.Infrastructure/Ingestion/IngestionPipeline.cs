using System.Security.Cryptography;
using System.Text;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AssistantApi.Application.Configuration;

namespace AssistantApi.Infrastructure.Ingestion;

public class IngestionPipeline : IIngestionPipeline
{
    private readonly IMetadataRepository _metadata;
    private readonly IFileHashRepository _fileHashes;
    private readonly IVectorRepository _vectors;
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly EmbeddingPipeline _embedder;
    private readonly AssistantOptions _options;
    private readonly ILogger<IngestionPipeline> _logger;

    private const string CodeCollection = "code-embeddings";
    private const string DocCollection = "doc-embeddings";
    private const string InstructionCollection = "instruction-embeddings";

    public IngestionPipeline(
        IMetadataRepository metadata,
        IFileHashRepository fileHashes,
        IVectorRepository vectors,
        IEnumerable<IDocumentParser> parsers,
        EmbeddingPipeline embedder,
        IOptions<AssistantOptions> options,
        ILogger<IngestionPipeline> logger)
    {
        _metadata = metadata;
        _fileHashes = fileHashes;
        _vectors = vectors;
        _parsers = parsers;
        _embedder = embedder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task IngestRepositoryAsync(
        Guid jobId, Guid repositoryId, string localPath, string repoName, string branch,
        CancellationToken ct = default)
    {
        var job = await _metadata.GetJobAsync(jobId, ct);
        if (job is null) return;

        var files = FileFilter.GetIndexableFiles(localPath).ToList();
        job.TotalFiles = files.Count;
        job.Status = IngestionJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await _metadata.UpdateJobAsync(job, ct);

        _logger.LogInformation("Ingesting {FileCount} files from {RepoName}", files.Count, repoName);

        var existingHashes = (await _fileHashes.GetByRepositoryAsync(repositoryId, ct))
            .ToDictionary(h => h.FilePath);

        // Parse all files in parallel (I/O bound — safe to parallelize)
        // Then embed in sequential batches (Ollama is the bottleneck, no benefit parallelising embed calls)
        var parsedFiles = await ParseFilesInParallelAsync(files, localPath, existingHashes, ct);

        _logger.LogInformation("Parsed {Changed} changed files out of {Total} total for {RepoName}",
            parsedFiles.Count, files.Count, repoName);

        // Collect all chunks across all changed files into one big list and embed in one pass
        var allItems = parsedFiles
            .SelectMany(f => f.Chunks.Select(c => (
                Id: MakePointId(repositoryId.ToString(), f.RelativePath, c.ChunkIndex.ToString()),
                Text: c.Content,
                Metadata: BuildCodeMetadata(c.Metadata, repoName, branch, f.RelativePath)
            )))
            .ToList();

        // Delete old vectors for changed files that previously existed
        foreach (var f in parsedFiles.Where(f => existingHashes.ContainsKey(f.RelativePath)))
        {
            await _vectors.DeleteByFilterAsync(CodeCollection, new Dictionary<string, string>
            {
                ["repository"] = repoName,
                ["file_path"] = f.RelativePath
            }, ct);
        }

        await _embedder.EmbedAndUpsertAsync(_options.EmbeddingModel, CodeCollection, allItems, ct);

        // Persist file hashes for all successfully parsed files
        foreach (var f in parsedFiles)
        {
            await _fileHashes.UpsertAsync(new FileHash
            {
                RepositoryId = repositoryId,
                FilePath = f.RelativePath,
                Hash = f.Hash,
                ChunkCount = f.Chunks.Count
            }, ct);
        }

        var processedFiles = parsedFiles.Count + (files.Count - parsedFiles.Count); // unchanged files count too
        job.ProcessedFiles = processedFiles;

        // Remove vectors for deleted files
        var currentRelativePaths = files.Select(f => Path.GetRelativePath(localPath, f)).ToHashSet();
        foreach (var deleted in existingHashes.Keys.Where(k => !currentRelativePaths.Contains(k)))
        {
            await _vectors.DeleteByFilterAsync(CodeCollection, new Dictionary<string, string>
            {
                ["repository"] = repoName,
                ["file_path"] = deleted
            }, ct);
            _logger.LogDebug("Removed vectors for deleted file {FilePath}", deleted);
        }

        job.Status = IngestionJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.ProcessedFiles = processedFiles;
        await _metadata.UpdateJobAsync(job, ct);

        _logger.LogInformation("Repository {RepoName} ingestion complete. {ProcessedFiles}/{TotalFiles} files processed",
            repoName, processedFiles, files.Count);
    }

    public async Task IngestDocumentAsync(
        Guid jobId, string filePath, string fileName, DocumentCollection collection,
        CancellationToken ct = default)
    {
        var job = await _metadata.GetJobAsync(jobId, ct);
        if (job is null) return;

        job.Status = IngestionJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        job.TotalFiles = 1;
        await _metadata.UpdateJobAsync(job, ct);

        try
        {
            // Deduplicate by content hash — skip re-embedding if the file hasn't changed.
            // Guid.Empty is the sentinel repository ID for documents and instructions
            // (they are not scoped to a repository).
            var content = await File.ReadAllTextAsync(filePath, ct);
            var newHash = ComputeHash(content);
            var existing = await _fileHashes.GetAsync(Guid.Empty, fileName, ct);

            if (existing is not null && existing.Hash == newHash)
            {
                _logger.LogInformation(
                    "Skipping {FileName} — content unchanged (hash {Hash})", fileName, newHash);
                job.Status = IngestionJobStatus.Completed;
                job.ProcessedFiles = 0;
                job.ErrorMessage = "Skipped — identical file already ingested.";
                job.CompletedAt = DateTime.UtcNow;
                await _metadata.UpdateJobAsync(job, ct);
                return;
            }

            var ext = Path.GetExtension(filePath);
            var parser = _parsers.FirstOrDefault(p => p.CanParse(ext))
                ?? throw new InvalidOperationException($"No parser found for extension {ext}");

            var chunks = await parser.ParseAsync(filePath, ct);
            var qdrantCollection = collection switch
            {
                DocumentCollection.Documents => DocCollection,
                DocumentCollection.Instructions => InstructionCollection,
                _ => CodeCollection
            };

            var items = chunks.Select(c => (
                Id: MakePointId(fileName, c.ChunkIndex.ToString()),
                Text: c.Content,
                Metadata: BuildDocMetadata(c.Metadata, fileName, collection)
            )).ToList();

            await _embedder.EmbedAndUpsertAsync(_options.EmbeddingModel, qdrantCollection, items, ct);

            // Persist the new hash so subsequent identical uploads are skipped.
            await _fileHashes.UpsertAsync(new FileHash
            {
                RepositoryId = Guid.Empty,
                FilePath     = fileName,
                Hash         = newHash,
                ChunkCount   = chunks.Count
            }, ct);

            job.Status = IngestionJobStatus.Completed;
            job.ProcessedFiles = 1;
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document ingestion failed for {FileName}", fileName);
            job.Status = IngestionJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
        }

        await _metadata.UpdateJobAsync(job, ct);
    }

    private static Dictionary<string, string> BuildCodeMetadata(
        Dictionary<string, string> parsed, string repo, string branch, string filePath)
    {
        var meta = new Dictionary<string, string>(parsed)
        {
            ["repository"] = repo,
            ["branch"] = branch,
            ["file_path"] = filePath,
            ["language"] = parsed.GetValueOrDefault("language", "text")
        };
        return meta;
    }

    private static Dictionary<string, string> BuildDocMetadata(
        Dictionary<string, string> parsed, string fileName, DocumentCollection collection)
    {
        var meta = new Dictionary<string, string>(parsed)
        {
            ["source"] = fileName,
            ["collection_type"] = collection.ToString().ToLowerInvariant(),
            ["doc_type"] = parsed.GetValueOrDefault("doc_type", "document")
        };
        return meta;
    }

    private async Task<List<ParsedFile>> ParseFilesInParallelAsync(
        List<string> files,
        string localPath,
        Dictionary<string, FileHash> existingHashes,
        CancellationToken ct)
    {
        // Limit parallelism to avoid overwhelming disk I/O
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
        var results = new System.Collections.Concurrent.ConcurrentBag<ParsedFile>();

        await Parallel.ForEachAsync(files, ct, async (filePath, token) =>
        {
            await semaphore.WaitAsync(token);
            try
            {
                var relativePath = Path.GetRelativePath(localPath, filePath);
                var fileContent = await File.ReadAllTextAsync(filePath, token);
                var hash = ComputeHash(fileContent);

                // Skip unchanged files
                if (existingHashes.TryGetValue(relativePath, out var existing) && existing.Hash == hash)
                    return;

                var ext = Path.GetExtension(filePath);
                var parser = _parsers.FirstOrDefault(p => p.CanParse(ext));
                if (parser is null) return;

                var chunks = await parser.ParseAsync(filePath, token);
                if (chunks.Count > 0)
                    results.Add(new ParsedFile(relativePath, hash, chunks));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse {FilePath}", filePath);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return results.ToList();
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    // Derives a deterministic UUID from composite key parts using SHA-256.
    // Qdrant requires valid UUID format for point IDs.
    private static string MakePointId(params string[] parts)
    {
        var combined = string.Join("|", parts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        // Use first 16 bytes of hash to build a valid UUID (version 4 format)
        var guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // version 4
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // variant bits
        return new Guid(guidBytes).ToString();
    }

    private record ParsedFile(string RelativePath, string Hash, IReadOnlyList<ParsedChunk> Chunks);
}
