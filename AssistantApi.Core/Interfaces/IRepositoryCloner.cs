namespace AssistantApi.Core.Interfaces;

public interface IRepositoryCloner
{
    /// <summary>
    /// Clones or fetches (if already cached) the given repository.
    /// Returns the local path where the repo is available.
    /// </summary>
    Task<string> CloneOrFetchAsync(string url, string branch, string? pat, CancellationToken ct = default);
}
