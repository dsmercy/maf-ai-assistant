using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

public class AgentContext
{
    public required string UserMessage { get; init; }
    public required string ConversationId { get; init; }
    public string UserId { get; init; } = "anonymous";
    public AgentIntent Intent { get; set; } = AgentIntent.GeneralQuestion;
    public List<RetrievedChunk> RetrievedChunks { get; set; } = [];
    public List<string> InstructionRules { get; set; } = [];
    public string? RepositoryFilter { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public class RetrievedChunk
{
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double Score { get; set; }

    public static RetrievedChunk FromSearchResult(VectorSearchResult r) => new()
    {
        Content = r.Content,
        FilePath = r.Metadata.GetValueOrDefault("file_path", string.Empty),
        Repository = r.Metadata.GetValueOrDefault("repository", string.Empty),
        Language = r.Metadata.GetValueOrDefault("language", string.Empty),
        Score = r.Score
    };
}

public class AgentResult
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }
    public AgentIntent Intent { get; set; }
}
