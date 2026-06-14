using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using AssistantApi.Infrastructure.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssistantApi;

/// <summary>
/// Background worker that polls PostgreSQL every 10 seconds for queued ingestion jobs
/// and processes them one at a time inside the API process.
///
/// Job types dispatched:
///   GitRepository   → clone/fetch repo, parse, embed → code-embeddings
///   ZipUpload       → extract archive, then same as GitRepository
///   Document        → parse, embed → doc-embeddings
///   InstructionFile → parse, embed, LLM-categorise → instruction-embeddings + DocumentTags
///
/// Runs in its own DI scope per job so DbContext is properly scoped.
/// Stuck "Running" jobs from a previous crash are reset to "Queued" on startup.
/// </summary>
public class IngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(IServiceScopeFactory scopeFactory, ILogger<IngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IngestionWorker started");

        using (var scope = _scopeFactory.CreateScope())
        {
            var metadata = scope.ServiceProvider.GetRequiredService<IMetadataRepository>();
            await metadata.ResetStuckJobsAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in IngestionWorker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("IngestionWorker stopped");
    }

    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        using var scope  = _scopeFactory.CreateScope();
        var metadata     = scope.ServiceProvider.GetRequiredService<IMetadataRepository>();
        var pipeline     = scope.ServiceProvider.GetRequiredService<IIngestionPipeline>();
        var cloner       = scope.ServiceProvider.GetRequiredService<IRepositoryCloner>();

        var jobs = await metadata.GetQueuedJobsAsync(ct);
        var job  = jobs.FirstOrDefault();
        if (job is null) return;

        _logger.LogInformation("Processing ingestion job {JobId} ({JobType})", job.Id, job.JobType);

        job.Status    = IngestionJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await metadata.UpdateJobAsync(job, ct);

        try
        {
            switch (job.JobType)
            {
                case IngestionJobType.GitRepository:
                    await ProcessGitJobAsync(job, metadata, pipeline, cloner, ct);
                    break;

                case IngestionJobType.ZipUpload:
                    await pipeline.IngestRepositoryAsync(
                        job.Id, job.RepositoryId!.Value,
                        job.SourcePath!, Path.GetFileName(job.SourcePath!), "upload", ct);
                    break;

                case IngestionJobType.Document:
                    await pipeline.IngestDocumentAsync(
                        job.Id, job.SourcePath!,
                        job.OriginalFileName ?? Path.GetFileName(job.SourcePath!),
                        DocumentCollection.Documents, ct);
                    break;

                case IngestionJobType.InstructionFile:
                    await pipeline.IngestDocumentAsync(
                        job.Id, job.SourcePath!,
                        job.OriginalFileName ?? Path.GetFileName(job.SourcePath!),
                        DocumentCollection.Instructions, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion job {JobId} failed", job.Id);
            job.Status       = IngestionJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt  = DateTime.UtcNow;
            await metadata.UpdateJobAsync(job, ct);
        }
    }

    private static async Task ProcessGitJobAsync(
        IngestionJob job,
        IMetadataRepository metadata,
        IIngestionPipeline pipeline,
        IRepositoryCloner cloner,
        CancellationToken ct)
    {
        var repo = await metadata.GetRepositoryAsync(job.RepositoryId!.Value, ct)
            ?? throw new InvalidOperationException($"Repository {job.RepositoryId} not found");

        var localPath = await cloner.CloneOrFetchAsync(repo.Url, repo.Branch, repo.Pat, ct);

        repo.LocalPath = localPath;
        repo.Status    = IndexingStatus.Indexing;
        await metadata.UpdateRepositoryAsync(repo, ct);

        await pipeline.IngestRepositoryAsync(job.Id, repo.Id, localPath, repo.Name, repo.Branch, ct);

        repo.Status        = IndexingStatus.Completed;
        repo.LastIndexedAt = DateTime.UtcNow;
        await metadata.UpdateRepositoryAsync(repo, ct);
    }
}
