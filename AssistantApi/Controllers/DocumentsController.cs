using AssistantApi.Application.DTOs;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMetadataRepository _metadata;
    private readonly ILogger<DocumentsController> _logger;

    private static readonly HashSet<string> AllowedDocExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".md", ".txt" };

    public DocumentsController(IMetadataRepository metadata, ILogger<DocumentsController> logger)
    {
        _metadata = metadata;
        _logger = logger;
    }

    [HttpPost]
    public Task<ActionResult<IngestionJobResponse>> UploadDocument(IFormFile file, CancellationToken ct)
        => UploadInternal(file, IngestionJobType.Document, ct);

    [HttpPost("/api/instructions")]
    public Task<ActionResult<IngestionJobResponse>> UploadInstruction(IFormFile file, CancellationToken ct)
        => UploadInternal(file, IngestionJobType.InstructionFile, ct);

    private async Task<ActionResult<IngestionJobResponse>> UploadInternal(
        IFormFile file, IngestionJobType jobType, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedDocExtensions.Contains(ext))
            return BadRequest($"Unsupported file type '{ext}'. Allowed: {string.Join(", ", AllowedDocExtensions)}");

        var uploadDir = "/data/uploads";
        Directory.CreateDirectory(uploadDir);
        var destPath = Path.Combine(uploadDir, $"{Guid.NewGuid()}{ext}");

        await using (var fs = System.IO.File.Create(destPath))
            await file.CopyToAsync(fs, ct);

        var job = new IngestionJob
        {
            JobType = jobType,
            Status = IngestionJobStatus.Queued,
            SourcePath = destPath
        };
        await _metadata.AddJobAsync(job, ct);

        _logger.LogInformation("{JobType} job {JobId} queued for {FileName}", jobType, job.Id, file.FileName);

        return Accepted(new IngestionJobResponse
        {
            JobId = job.Id,
            Status = "Queued",
            Message = $"'{file.FileName}' queued for ingestion."
        });
    }
}
