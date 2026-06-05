using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

/// <summary>
/// Admin endpoints for managing prompt templates, feature flags, and audit logs.
/// All endpoints under this controller are intended for administrators only.
/// In production, protect these with an admin role claim check.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IPromptTemplateRepository _templates;
    private readonly IFeatureFlagRepository _flags;
    private readonly IAuditLogRepository _auditLogs;

    public AdminController(
        IPromptTemplateRepository templates,
        IFeatureFlagRepository flags,
        IAuditLogRepository auditLogs)
    {
        _templates = templates;
        _flags = flags;
        _auditLogs = auditLogs;
    }

    // ── Prompt Templates ────────────────────────────────────────────────────

    /// <summary>Returns all prompt templates stored in the database.</summary>
    [HttpGet("prompt-templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
        => Ok(await _templates.GetAllAsync(ct));

    /// <summary>
    /// Creates or updates a prompt template for the specified task type.
    /// Placeholders: {instructions}, {context_chunks}, {user_message}, {language}.
    /// </summary>
    [HttpPut("prompt-templates/{taskType}")]
    public async Task<IActionResult> UpsertTemplate(string taskType, [FromBody] UpsertTemplateRequest request, CancellationToken ct)
    {
        var template = new PromptTemplate
        {
            Name = request.Name,
            TaskType = taskType,
            SystemPrompt = request.SystemPrompt,
            UserPromptTemplate = request.UserPromptTemplate,
            IsActive = request.IsActive
        };
        await _templates.UpsertAsync(template, ct);
        return Ok(template);
    }

    // ── Feature Flags ────────────────────────────────────────────────────────

    /// <summary>Returns all feature flags and their current enabled state.</summary>
    [HttpGet("feature-flags")]
    public async Task<IActionResult> GetFlags(CancellationToken ct)
        => Ok(await _flags.GetAllAsync(ct));

    /// <summary>Creates or updates a feature flag by name.</summary>
    [HttpPut("feature-flags/{name}")]
    public async Task<IActionResult> UpsertFlag(string name, [FromBody] UpsertFlagRequest request, CancellationToken ct)
    {
        var flag = new FeatureFlag
        {
            Name = name,
            IsEnabled = request.IsEnabled,
            Description = request.Description ?? string.Empty
        };
        await _flags.UpsertAsync(flag, ct);
        return Ok(flag);
    }

    // ── Audit Logs ───────────────────────────────────────────────────────────

    /// <summary>Returns the most recent audit log entries ordered by newest first.</summary>
    /// <param name="limit">Maximum number of entries to return (default 100).</param>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 100, CancellationToken ct = default)
        => Ok(await _auditLogs.GetRecentAsync(limit, ct));
}

public class UpsertTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class UpsertFlagRequest
{
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
}
