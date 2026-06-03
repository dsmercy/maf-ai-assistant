using AssistantApi.Application.Configuration;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

public class RepositoryAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly AssistantOptions _options;
    private readonly ILogger<RepositoryAgent> _logger;

    public string Name => "RepositoryAgent";

    public RepositoryAgent(
        IOllamaClient ollama,
        IVectorRepository vectors,
        IOptions<AssistantOptions> options,
        ILogger<RepositoryAgent> logger)
    {
        _ollama = ollama;
        _vectors = vectors;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            _logger.LogInformation("RepositoryAgent embedding query for conversation {ConversationId}",
                context.ConversationId);

            var vector = await _ollama.EmbedAsync(_options.EmbeddingModel, context.UserMessage,
                context.CancellationToken);

            var filters = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(context.RepositoryFilter))
                filters["repository"] = context.RepositoryFilter;

            var results = await _vectors.SearchAsync(
                "code-embeddings", vector, _options.TopK,
                filters.Count > 0 ? filters : null,
                context.CancellationToken);

            context.RetrievedChunks = results
                .Select(RetrievedChunk.FromSearchResult)
                .ToList();

            _logger.LogInformation("RepositoryAgent retrieved {Count} chunks for conversation {ConversationId}",
                context.RetrievedChunks.Count, context.ConversationId);

            return new AgentResult { Success = true, Response = string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RepositoryAgent failed for conversation {ConversationId}", context.ConversationId);
            // Non-fatal — continue without repository context
            return new AgentResult { Success = false, Response = string.Empty, ErrorMessage = ex.Message };
        }
    }
}
