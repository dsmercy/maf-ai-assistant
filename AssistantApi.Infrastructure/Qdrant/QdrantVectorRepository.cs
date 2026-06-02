using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AssistantApi.Infrastructure.Qdrant;

public class QdrantVectorRepository : IVectorRepository
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorRepository> _logger;

    public QdrantVectorRepository(QdrantClient client, ILogger<QdrantVectorRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task UpsertAsync(string collection, VectorPoint point, CancellationToken ct = default)
        => await UpsertBatchAsync(collection, [point], ct);

    public async Task UpsertBatchAsync(string collection, IEnumerable<VectorPoint> points, CancellationToken ct = default)
    {
        var qdrantPoints = points.Select(p =>
        {
            var payload = p.Metadata.ToDictionary(
                kv => kv.Key,
                kv => (Value)kv.Value);
            payload["_content"] = p.Content;

            return new PointStruct
            {
                Id = new PointId { Uuid = p.Id },
                Vectors = p.Vector,
                Payload = { payload }
            };
        }).ToList();

        await _client.UpsertAsync(collection, qdrantPoints, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection,
        float[] vector,
        int topK = 5,
        Dictionary<string, string>? filters = null,
        CancellationToken ct = default)
    {
        Filter? filter = null;
        if (filters is { Count: > 0 })
        {
            filter = new Filter
            {
                Must =
                {
                    filters.Select(kv => new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = kv.Key,
                            Match = new Match { Keyword = kv.Value }
                        }
                    })
                }
            };
        }

        var results = await _client.SearchAsync(collection, vector, limit: (ulong)topK, filter: filter, cancellationToken: ct);

        return results.Select(r =>
        {
            var meta = r.Payload.ToDictionary(kv => kv.Key, kv => kv.Value.StringValue);
            meta.TryGetValue("_content", out var content);
            meta.Remove("_content");

            return new VectorSearchResult
            {
                Id = r.Id.Uuid,
                Score = r.Score,
                Metadata = meta,
                Content = content ?? string.Empty
            };
        }).ToList();
    }

    public async Task DeleteByFilterAsync(string collection, Dictionary<string, string> filters, CancellationToken ct = default)
    {
        var filter = new Filter
        {
            Must =
            {
                filters.Select(kv => new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = kv.Key,
                        Match = new Match { Keyword = kv.Value }
                    }
                })
            }
        };

        await _client.DeleteAsync(collection, filter, cancellationToken: ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var info = await _client.HealthAsync(cancellationToken: ct);
            return !string.IsNullOrEmpty(info.Version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant health check failed");
            return false;
        }
    }
}
