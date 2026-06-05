using AssistantApi.Application.DTOs;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

/// <summary>
/// Provides visibility into the status of background ingestion jobs.
/// Jobs are created when repositories or documents are registered for indexing
/// and executed asynchronously by the IngestionService worker.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IMetadataRepository _metadata;

    public JobsController(IMetadataRepository metadata) => _metadata = metadata;

    /// <summary>
    /// Returns all recent ingestion jobs ordered by creation time (newest first).
    /// Includes a summary of counts by status (queued, running, completed, failed)
    /// and per-job details including progress percentage and duration.
    /// </summary>
    /// <param name="limit">Maximum number of jobs to return. Defaults to 50.</param>
    [HttpGet]
    public async Task<ActionResult<JobSummaryResponse>> GetAll(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var jobs = await _metadata.GetAllJobsAsync(limit, ct);
        var mapped = jobs.Select(Map).ToList();

        return Ok(new JobSummaryResponse
        {
            Total = mapped.Count,
            Queued = mapped.Count(j => j.Status == nameof(IngestionJobStatus.Queued)),
            Running = mapped.Count(j => j.Status == nameof(IngestionJobStatus.Running)),
            Completed = mapped.Count(j => j.Status == nameof(IngestionJobStatus.Completed)),
            Failed = mapped.Count(j => j.Status == nameof(IngestionJobStatus.Failed)),
            Jobs = mapped
        });
    }

    /// <summary>
    /// Returns a single ingestion job by its ID.
    /// Useful for polling the status of a specific job after registration.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobStatusResponse>> GetById(Guid id, CancellationToken ct)
    {
        var job = await _metadata.GetJobAsync(id, ct);
        if (job is null) return NotFound(new { error = $"Job {id} not found." });
        return Ok(Map(job));
    }

    private static JobStatusResponse Map(IngestionJob j) => new()
    {
        Id = j.Id,
        RepositoryId = j.RepositoryId,
        JobType = j.JobType.ToString(),
        Status = j.Status.ToString(),
        ProcessedFiles = j.ProcessedFiles,
        TotalFiles = j.TotalFiles,
        CreatedAt = j.CreatedAt,
        StartedAt = j.StartedAt,
        CompletedAt = j.CompletedAt,
        ErrorMessage = j.ErrorMessage,
        SourcePath = j.SourcePath
    };
}
