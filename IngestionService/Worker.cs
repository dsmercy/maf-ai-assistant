using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngestionService;

/// <summary>
/// Background worker that dequeues and processes ingestion jobs.
/// Full pipeline (clone, chunk, embed, upsert) implemented in Phase 3.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IMetadataRepository _metadata;
    private readonly ILogger<Worker> _logger;

    public Worker(IMetadataRepository metadata, ILogger<Worker> logger)
    {
        _metadata = metadata;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IngestionService worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Phase 3: poll for Queued jobs and process them
                _logger.LogDebug("IngestionService polling for queued jobs...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ingestion worker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("IngestionService worker stopped");
    }
}
