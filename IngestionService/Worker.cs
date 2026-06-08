using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using AssistantApi.Infrastructure.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngestionService;

/// <summary>
/// Background worker service that polls PostgreSQL for queued ingestion jobs
/// and processes them one at a time.
///
/// On startup, any jobs stuck in "Running" state (from a previous crashed instance)
/// are reset to "Queued" so they are retried automatically.
///
/// The polling interval is 10 seconds. Each tick dequeues the oldest queued job
/// and dispatches it to the appropriate ingestion handler based on job type:
///   - GitRepository  → clone/fetch the repo, parse files, embed, upsert to Qdrant
///   - ZipUpload      → extract the archive, then same as GitRepository
///   - Document       → parse and embed into doc-embeddings
///   - InstructionFile → parse and embed into instruction-embeddings
///
/// Each job runs in its own DI scope so DbContext and services are properly scoped.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Main worker loop. Resets stuck jobs on startup, then polls every 10 seconds
    /// for new queued jobs and processes them.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IngestionService worker started");

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
                _logger.LogError(ex, "Unhandled error in ingestion worker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("IngestionService worker stopped");
    }

    /// <summary>
    /// Dequeues the next pending job from PostgreSQL and dispatches it to the
    /// appropriate ingestion handler. Creates a new DI scope per job execution.
    /// </summary>
    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var metadata = scope.ServiceProvider.GetRequiredService<IMetadataRepository>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipeline>();
        var cloner = scope.ServiceProvider.GetRequiredService<IRepositoryCloner>();

        var jobs = await metadata.GetQueuedJobsAsync(ct);
        var job = jobs.FirstOrDefault();
        if (job is null) return;

        _logger.LogInformation("Processing ingestion job {JobId} of type {JobType}", job.Id, job.JobType);

        job.Status = IngestionJobStatus.Running;
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
            job.Status = IngestionJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            await metadata.UpdateJobAsync(job, ct);
        }
    }

    /// <summary>
    /// Handles a GitRepository job: clones or fetches the repo via LibGit2Sharp,
    /// updates the repository status, then delegates to IngestionPipeline for
    /// parsing, embedding, and Qdrant upsert.
    /// </summary>
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
        repo.Status = IndexingStatus.Indexing;
        await metadata.UpdateRepositoryAsync(repo, ct);

        await pipeline.IngestRepositoryAsync(job.Id, repo.Id, localPath, repo.Name, repo.Branch, ct);

        repo.Status = IndexingStatus.Completed;
        repo.LastIndexedAt = DateTime.UtcNow;
        await metadata.UpdateRepositoryAsync(repo, ct);
    }
}
