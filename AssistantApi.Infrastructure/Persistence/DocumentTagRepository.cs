using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

public class DocumentTagRepository : IDocumentTagRepository
{
    private readonly AssistantDbContext _db;

    public DocumentTagRepository(AssistantDbContext db) => _db = db;

    public async Task UpsertAsync(DocumentTag tag, CancellationToken ct = default)
    {
        var existing = await _db.DocumentTags
            .FirstOrDefaultAsync(t => t.PointId == tag.PointId, ct);

        if (existing is null)
        {
            _db.DocumentTags.Add(tag);
        }
        else
        {
            existing.Language  = tag.Language;
            existing.Category  = tag.Category;
            existing.Keywords  = tag.Keywords;
            existing.Summary   = tag.Summary;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentTag>> GetAllAsync(CancellationToken ct = default) =>
        await _db.DocumentTags.AsNoTracking().ToListAsync(ct);

    public async Task DeleteBySourceFileAsync(string sourceFile, CancellationToken ct = default)
    {
        await _db.DocumentTags
            .Where(t => t.SourceFile == sourceFile)
            .ExecuteDeleteAsync(ct);
    }
}
