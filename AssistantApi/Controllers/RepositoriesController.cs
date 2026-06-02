using AssistantApi.Application.DTOs;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoriesController : ControllerBase
{
    private readonly IMetadataRepository _metadata;
    private readonly IValidator<RegisterRepositoryRequest> _validator;
    private readonly ILogger<RepositoriesController> _logger;

    public RepositoriesController(
        IMetadataRepository metadata,
        IValidator<RegisterRepositoryRequest> validator,
        ILogger<RepositoriesController> logger)
    {
        _metadata = metadata;
        _validator = validator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RepositoryResponse>>> GetAll(CancellationToken ct)
    {
        var repos = await _metadata.GetAllRepositoriesAsync(ct);
        return Ok(repos.Select(MapToResponse));
    }

    [HttpPost]
    public async Task<ActionResult<IngestionJobResponse>> Register(
        [FromBody] RegisterRepositoryRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

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
    public async Task<ActionResult<IngestionJobResponse>> Upload(
        IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".zip")
            return BadRequest("Only .zip files are supported for repository upload.");

        var uploadDir = "/data/uploads";
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var repo = await _metadata.GetRepositoryAsync(id, ct);
        if (repo is null) return NotFound();

        await _metadata.DeleteRepositoryAsync(id, ct);
        return NoContent();
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
