using AssistantApi.Application.DTOs;
using AssistantApi.Validators;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

/// <summary>
/// Handles uploading documents and instruction files for ingestion into Qdrant.
/// Documents are stored in doc-embeddings; instruction files in instruction-embeddings.
/// File size, extension, and MIME type are validated before accepting the upload.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IMetadataRepository _metadata;
    private readonly ILogger<DocumentsController> _logger;

    private static readonly string[] AllowedDocExtensions = [".pdf", ".docx", ".md", ".txt"];

    public DocumentsController(IMetadataRepository metadata, ILogger<DocumentsController> logger)
    {
        _metadata = metadata;
        _logger = logger;
    }

    /// <summary>Uploads a PDF, DOCX, MD, or TXT document for ingestion into doc-embeddings.</summary>
    [HttpPost]
    public Task<ActionResult<IngestionJobResponse>> UploadDocument(IFormFile file, CancellationToken ct)
        => UploadInternal(file, IngestionJobType.Document, AllowedDocExtensions, ct);

    /// <summary>Uploads a coding standards / rules file for ingestion into instruction-embeddings.</summary>
    [HttpPost("/api/instructions")]
    public Task<ActionResult<IngestionJobResponse>> UploadInstruction(IFormFile file, CancellationToken ct)
        => UploadInternal(file, IngestionJobType.InstructionFile, AllowedDocExtensions, ct);

    private async Task<ActionResult<IngestionJobResponse>> UploadInternal(
        IFormFile file, IngestionJobType jobType, string[] allowedExtensions, CancellationToken ct)
    {
        var errors = FileUploadValidator.Validate(file, allowedExtensions);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
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
