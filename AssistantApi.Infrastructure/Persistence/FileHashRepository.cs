using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

public class FileHashRepository : IFileHashRepository
{
    private readonly AssistantDbContext _db;

    public FileHashRepository(AssistantDbContext db) => _db = db;

    public Task<FileHash?> GetAsync(Guid repositoryId, string filePath, CancellationToken ct = default)
        => _db.FileHashes.FirstOrDefaultAsync(f => f.RepositoryId == repositoryId && f.FilePath == filePath, ct);

    public async Task UpsertAsync(FileHash fileHash, CancellationToken ct = default)
    {
        var existing = await GetAsync(fileHash.RepositoryId, fileHash.FilePath, ct);
        if (existing is null)
            _db.FileHashes.Add(fileHash);
        else
        {
            existing.Hash = fileHash.Hash;
            existing.ChunkCount = fileHash.ChunkCount;
            existing.IndexedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FileHash>> GetByRepositoryAsync(Guid repositoryId, CancellationToken ct = default)
        => await _db.FileHashes.Where(f => f.RepositoryId == repositoryId).ToListAsync(ct);

    public async Task DeleteByRepositoryAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var hashes = await _db.FileHashes.Where(f => f.RepositoryId == repositoryId).ToListAsync(ct);
        _db.FileHashes.RemoveRange(hashes);
        await _db.SaveChangesAsync(ct);
    }
}
