namespace AssistantApi.Core.Interfaces;

public interface IVectorRepository
{
    Task UpsertAsync(string collection, VectorPoint point, CancellationToken ct = default);
    Task UpsertBatchAsync(string collection, IEnumerable<VectorPoint> points, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collection, float[] vector, int topK = 5, Dictionary<string, string>? filters = null, CancellationToken ct = default);
    Task DeleteByFilterAsync(string collection, Dictionary<string, string> filters, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public class VectorPoint
{
    public required string Id { get; init; }
    public required float[] Vector { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
    public string Content { get; init; } = string.Empty;
}

public class VectorSearchResult
{
    public required string Id { get; init; }
    public double Score { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
    public string Content { get; init; } = string.Empty;
}
