using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using AssistantApi.Infrastructure.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngestionService;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IngestionService worker started");

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

    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var metadata = scope.ServiceProvider.GetRequiredService<IMetadataRepository>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipeline>();
        var cloner = scope.ServiceProvider.GetRequiredService<IRepositoryCloner>();

        // Find next queued job
        // We query via the repository — a proper queue (e.g. outbox) can replace this in Phase 6
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
                        job.Id, job.SourcePath!, Path.GetFileName(job.SourcePath!),
                        DocumentCollection.Documents, ct);
                    break;

                case IngestionJobType.InstructionFile:
                    await pipeline.IngestDocumentAsync(
                        job.Id, job.SourcePath!, Path.GetFileName(job.SourcePath!),
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
