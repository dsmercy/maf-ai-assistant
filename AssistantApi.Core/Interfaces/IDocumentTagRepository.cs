using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

public interface IDocumentTagRepository
{
    /// <summary>Upserts a tag row (insert or update by PointId).</summary>
    Task UpsertAsync(DocumentTag tag, CancellationToken ct = default);

    /// <summary>Returns all distinct tag rows — used to rebuild the vocabulary cache.</summary>
    Task<IReadOnlyList<DocumentTag>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Deletes all tags for a given source file — called before re-ingesting.</summary>
    Task DeleteBySourceFileAsync(string sourceFile, CancellationToken ct = default);
}
