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

        var processedFiles = 0;

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(localPath, filePath);
            var fileContent = await File.ReadAllTextAsync(filePath, ct);
            var currentHash = ComputeHash(fileContent);

            // Skip unchanged files
            if (existingHashes.TryGetValue(relativePath, out var existing) && existing.Hash == currentHash)
            {
                processedFiles++;
                continue;
            }

            try
            {
                var ext = Path.GetExtension(filePath);
                var parser = _parsers.FirstOrDefault(p => p.CanParse(ext));
                if (parser is null)
                {
                    processedFiles++;
                    continue;
                }

                var chunks = await parser.ParseAsync(filePath, ct);

                // Remove old vectors for this file if it existed before
                if (existingHashes.ContainsKey(relativePath))
                {
                    await _vectors.DeleteByFilterAsync(CodeCollection, new Dictionary<string, string>
                    {
                        ["repository"] = repoName,
                        ["file_path"] = relativePath
                    }, ct);
                }

                var items = chunks.Select(c => (
                    Id: $"{repositoryId}:{relativePath}:{c.ChunkIndex}",
                    Text: c.Content,
                    Metadata: BuildCodeMetadata(c.Metadata, repoName, branch, relativePath)
                )).ToList();

                await _embedder.EmbedAndUpsertAsync(_options.EmbeddingModel, CodeCollection, items, ct);

                await _fileHashes.UpsertAsync(new FileHash
                {
                    RepositoryId = repositoryId,
                    FilePath = relativePath,
                    Hash = currentHash,
                    ChunkCount = chunks.Count
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process file {FilePath}", relativePath);
            }

            processedFiles++;
            job.ProcessedFiles = processedFiles;
            if (processedFiles % 50 == 0)
                await _metadata.UpdateJobAsync(job, ct);
        }

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
                Id: $"{fileName}:{c.ChunkIndex}:{Guid.NewGuid():N}",
                Text: c.Content,
                Metadata: BuildDocMetadata(c.Metadata, fileName, collection)
            )).ToList();

            await _embedder.EmbedAndUpsertAsync(_options.EmbeddingModel, qdrantCollection, items, ct);

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

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
