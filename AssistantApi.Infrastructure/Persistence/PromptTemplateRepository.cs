using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

public class PromptTemplateRepository : IPromptTemplateRepository
{
    private readonly AssistantDbContext _db;

    public PromptTemplateRepository(AssistantDbContext db) => _db = db;

    public Task<PromptTemplate?> GetByTaskTypeAsync(string taskType, CancellationToken ct = default)
        => _db.PromptTemplates.FirstOrDefaultAsync(t => t.TaskType == taskType && t.IsActive, ct);

    public async Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken ct = default)
        => await _db.PromptTemplates.OrderBy(t => t.TaskType).ToListAsync(ct);

    public async Task UpsertAsync(PromptTemplate template, CancellationToken ct = default)
    {
        var existing = await _db.PromptTemplates.FirstOrDefaultAsync(t => t.TaskType == template.TaskType, ct);
        if (existing is null)
            _db.PromptTemplates.Add(template);
        else
        {
            existing.SystemPrompt = template.SystemPrompt;
            existing.UserPromptTemplate = template.UserPromptTemplate;
            existing.Name = template.Name;
            existing.IsActive = template.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
