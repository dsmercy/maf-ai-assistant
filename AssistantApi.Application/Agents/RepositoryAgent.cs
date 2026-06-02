using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Stub: full RAG retrieval implemented in Phase 3/4.
/// </summary>
public class RepositoryAgent : IAgent
{
    private readonly ILogger<RepositoryAgent> _logger;

    public string Name => "RepositoryAgent";

    public RepositoryAgent(ILogger<RepositoryAgent> logger)
    {
        _logger = logger;
    }

    public Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        _logger.LogDebug("RepositoryAgent stub invoked for conversation {ConversationId}", context.ConversationId);
        // Phase 3: embed query, search Qdrant code-embeddings, populate context.RetrievedChunks
        return Task.FromResult(new AgentResult { Success = true, Response = string.Empty });
    }
}
