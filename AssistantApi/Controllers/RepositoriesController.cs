using AssistantApi.Application.DTOs;
using AssistantApi.Application.Validators;
using AssistantApi.Validators;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using AssistantApi.Infrastructure.Ingestion;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoriesController : ControllerBase
{
    private readonly IMetadataRepository _metadata;
    private readonly IVectorRepository _vectors;
    private readonly IFileHashRepository _fileHashes;
    private readonly IValidator<RegisterRepositoryRequest> _validator;
    private readonly IngestionOptions _ingestion;
    private readonly ILogger<RepositoriesController> _logger;

    public RepositoriesController(
        IMetadataRepository metadata,
        IVectorRepository vectors,
        IFileHashRepository fileHashes,
        IValidator<RegisterRepositoryRequest> validator,
        IOptions<IngestionOptions> ingestion,
        ILogger<RepositoriesController> logger)
    {
        _metadata = metadata;
        _vectors = vectors;
        _fileHashes = fileHashes;
        _validator = validator;
        _ingestion = ingestion.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RepositoryResponse>>> GetAll(CancellationToken ct)
    {
        var repos = await _metadata.GetAllRepositoriesAsync(ct);
        return Ok(repos.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RepositoryResponse>> GetById(Guid id, CancellationToken ct)
    {
        var repo = await _metadata.GetRepositoryAsync(id, ct);
        if (repo is null) return NotFound();
        return Ok(MapToResponse(repo));
    }

    [HttpPost]
    [EnableRateLimiting("ingestion")]
    public async Task<ActionResult<IngestionJobResponse>> Register(
        [FromBody] RegisterRepositoryRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        // Duplicate check — same URL + branch already registered
        var existing = await _metadata.GetRepositoryByUrlAndBranchAsync(request.Url, request.Branch, ct);
        if (existing is not null)
        {
            return Conflict(new
            {
                error = $"Repository '{existing.Name}' (branch: {existing.Branch}) is already registered.",
                existingId = existing.Id,
                status = existing.Status.ToString(),
                hint = $"DELETE /api/repositories/{existing.Id} to remove it first, then register again."
            });
        }

        var repoName = DeriveRepoName(request.Url);
        var repo = new Repository
        {
            Url = request.Url,
            Branch = request.Branch,
            Name = repoName,
            Pat = request.Pat,
            Status = IndexingStatus.Pending
        };
        await _metadata.AddRepositoryAsync(repo, ct);

        var job = new IngestionJob
        {
            RepositoryId = repo.Id,
            JobType = IngestionJobType.GitRepository,
            Status = IngestionJobStatus.Queued
        };
        await _metadata.AddJobAsync(job, ct);

        _logger.LogInformation("Repository {RepoName} registered, job {JobId} queued", repoName, job.Id);

        return Accepted(new IngestionJobResponse
        {
            JobId = job.Id,
            Status = "Queued",
            Message = $"Repository '{repoName}' queued for indexing."
        });
    }

    [HttpPost("upload")]
    public async Task<ActionResult<IngestionJobResponse>> Upload(IFormFile file, CancellationToken ct)
    {
        var errors = FileUploadValidator.Validate(file, [".zip"]);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uploadDir = _ingestion.UploadPath;
        Directory.CreateDirectory(uploadDir);
        var destPath = Path.Combine(uploadDir, $"{Guid.NewGuid()}{ext}");

        await using (var fs = System.IO.File.Create(destPath))
            await file.CopyToAsync(fs, ct);

        var job = new IngestionJob
        {
            JobType = IngestionJobType.ZipUpload,
            Status = IngestionJobStatus.Queued,
            SourcePath = destPath
        };
        await _metadata.AddJobAsync(job, ct);

        return Accepted(new IngestionJobResponse
        {
            JobId = job.Id,
            Status = "Queued",
            Message = $"File '{file.FileName}' uploaded and queued for indexing."
        });
    }

    /// <summary>
    /// Deletes a repository and removes all its vectors from Qdrant and file hashes from the DB.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var repo = await _metadata.GetRepositoryAsync(id, ct);
        if (repo is null) return NotFound(new { error = $"Repository {id} not found." });

        // Remove all vectors for this repository from Qdrant
        try
        {
            await _vectors.DeleteByFilterAsync("code-embeddings",
                new Dictionary<string, string> { ["repository"] = repo.Name }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Qdrant vectors for repo {RepoName} — continuing", repo.Name);
        }

        // Remove file hashes
        await _fileHashes.DeleteByRepositoryAsync(id, ct);

        // Remove metadata
        await _metadata.DeleteRepositoryAsync(id, ct);

        _logger.LogInformation("Repository {RepoName} ({Id}) deleted", repo.Name, id);
        return NoContent();
    }

    /// <summary>
    /// Convenience endpoint: deletes existing registration and immediately re-registers the same URL+branch.
    /// </summary>
    [HttpPost("{id:guid}/reindex")]
    public async Task<ActionResult<IngestionJobResponse>> Reindex(Guid id, CancellationToken ct)
    {
        var repo = await _metadata.GetRepositoryAsync(id, ct);
        if (repo is null) return NotFound(new { error = $"Repository {id} not found." });

        // Remove old vectors and hashes
        try
        {
            await _vectors.DeleteByFilterAsync("code-embeddings",
                new Dictionary<string, string> { ["repository"] = repo.Name }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Qdrant vectors for repo {RepoName}", repo.Name);
        }

        await _fileHashes.DeleteByRepositoryAsync(id, ct);

        // Reset repo status and queue a new job
        repo.Status = IndexingStatus.Pending;
        repo.ErrorMessage = null;
        repo.LastIndexedAt = null;
        await _metadata.UpdateRepositoryAsync(repo, ct);

        var job = new IngestionJob
        {
            RepositoryId = repo.Id,
            JobType = IngestionJobType.GitRepository,
            Status = IngestionJobStatus.Queued
        };
        await _metadata.AddJobAsync(job, ct);

        _logger.LogInformation("Repository {RepoName} queued for re-indexing, job {JobId}", repo.Name, job.Id);

        return Accepted(new IngestionJobResponse
        {
            JobId = job.Id,
            Status = "Queued",
            Message = $"Repository '{repo.Name}' queued for re-indexing."
        });
    }

    private static RepositoryResponse MapToResponse(Repository r) => new()
    {
        Id = r.Id,
        Url = r.Url,
        Name = r.Name,
        Branch = r.Branch,
        Status = r.Status.ToString(),
        FileCount = r.FileCount,
        ChunkCount = r.ChunkCount,
        CreatedAt = r.CreatedAt,
        LastIndexedAt = r.LastIndexedAt,
        ErrorMessage = r.ErrorMessage
    };

    private static string DeriveRepoName(string url)
    {
        var name = url.TrimEnd('/').Split('/').Last();
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name;
    }
}
