using AssistantApi.Application.Configuration;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Retrieves relevant source code chunks from the indexed repositories using semantic search.
/// This is the RAG (Retrieval-Augmented Generation) retrieval step.
///
/// Process:
///   1. Embed the user's message into a 768-dimensional vector via Ollama nomic-embed-text
///   2. Search the code-embeddings Qdrant collection for the most similar chunks
///   3. Optionally filter results by repository name (from AgentContext.RepositoryFilter)
///   4. Populate AgentContext.RetrievedChunks for CodingAgent to use in the prompt
///
/// Failures are non-fatal — if embedding or Qdrant search fails, the pipeline
/// continues with an empty chunks list and the LLM answers from general knowledge.
/// </summary>
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

    /// <summary>
    /// Embeds the user message and searches Qdrant for the top-K most relevant code chunks.
    /// Results are written into context.RetrievedChunks and returned to the orchestrator.
    /// </summary>
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
            return new AgentResult { Success = false, Response = string.Empty, ErrorMessage = ex.Message };
        }
    }
}
