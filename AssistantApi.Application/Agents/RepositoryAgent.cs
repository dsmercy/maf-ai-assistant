using AssistantApi.Application.Configuration;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Retrieves relevant context chunks from indexed collections using semantic search.
/// Which collections are searched is controlled by feature flags in PostgreSQL:

///
/// Process:
///   1. Embed the user's message into a 768-dimensional vector via Ollama nomic-embed-text
///   2. Search the code-embeddings Qdrant collection for the most similar chunks
///   3. Optionally filter results by repository name (from AgentContext.RepositoryFilter)
///   4. Populate AgentContext.RetrievedChunks for CodingAgent to use in the prompt
///
/// Failures are non-fatal — if embedding or Qdrant search fails, the pipeline
/// continues with an empty chunks list.
/// </summary>
public class RepositoryAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly IFeatureFlagRepository _flags;
    private readonly AssistantOptions _options;
    private readonly ILogger<RepositoryAgent> _logger;

    public string Name => "RepositoryAgent";

    public RepositoryAgent(
        IOllamaClient ollama,
        IVectorRepository vectors,
        IFeatureFlagRepository flags,
        IOptions<AssistantOptions> options,
        ILogger<RepositoryAgent> logger)
    {
        _ollama = ollama;
        _vectors = vectors;
        _flags = flags;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Checks which collections are enabled, embeds the user message once, then searches
    /// all enabled collections in parallel. Results are merged and written into
    /// context.RetrievedChunks for CodingAgent to use in the prompt.
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            // Read flags sequentially — DbContext is not thread-safe and cannot handle
            // concurrent queries on the same instance.
            var useCode = await _flags.IsEnabledAsync("code-embeddings", context.CancellationToken);
            var useDocs = await _flags.IsEnabledAsync("doc-embeddings",  context.CancellationToken);

            if (!useCode && !useDocs)
            {
                _logger.LogInformation(
                    "RepositoryAgent skipped — both code-embeddings and doc-embeddings are disabled " +
                    "(conversation {ConversationId})", context.ConversationId);
                context.RetrievedChunks = [];
                return new AgentResult { Success = true, Response = string.Empty };
            }

            _logger.LogInformation(
                "RepositoryAgent searching collections [code={UseCode}, docs={UseDocs}] " +
                "for conversation {ConversationId}", useCode, useDocs, context.ConversationId);

            var vector = await _ollama.EmbedAsync(_options.EmbeddingModel, context.UserMessage,
                context.CancellationToken);

            // Repository filter applies to code-embeddings only.
            var repoFilters = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(context.RepositoryFilter))
                repoFilters["repository"] = context.RepositoryFilter;

            // Launch searches for enabled collections in parallel.
            var searches = new List<Task<IReadOnlyList<VectorSearchResult>>>();

            if (useCode)
                searches.Add(_vectors.SearchAsync(
                    "code-embeddings", vector, _options.TopK,
                    repoFilters.Count > 0 ? repoFilters : null,
                    context.CancellationToken));

            if (useDocs)
                searches.Add(_vectors.SearchAsync(
                    "doc-embeddings", vector, _options.TopK,
                    null,
                    context.CancellationToken));

            await Task.WhenAll(searches);

            context.RetrievedChunks = searches
                .SelectMany(t => t.Result)
                .Select(RetrievedChunk.FromSearchResult)
                .ToList();

            _logger.LogInformation(
                "RepositoryAgent retrieved {Count} chunks for conversation {ConversationId}",
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
