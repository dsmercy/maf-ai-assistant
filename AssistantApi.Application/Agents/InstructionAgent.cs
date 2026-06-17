using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Retrieves relevant coding standards from instruction-embeddings using semantic search.
///
/// DYNAMIC TAG MATCHING
/// ====================
/// On every request:
///   1. Embed the user query (already done for the RAG search, reused here).
///   2. Fetch the tag vocabulary from ITagVocabularyCache (0 ms when warm).
///   3. Compute cosine similarity between the query embedding and each tag entry embedding.
///   4. Pick the top-N tags above a similarity threshold.
///   5. Run a Qdrant search filtered to those tags — no hardcoded maps needed.
///
/// The tag vocabulary is built automatically during instruction document ingestion:
/// the LLM classifies each chunk and writes language/category/keywords to DocumentTag rows.
/// TagVocabularyCache pre-embeds those rows and caches them for 5 minutes.
/// </summary>
public class InstructionAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly ITagVocabularyCache _tagCache;
    private readonly IFeatureFlagRepository _flags;
    private readonly AssistantOptions _options;
    private readonly ILogger<InstructionAgent> _logger;

    private const string InstructionCollection = "instruction-embeddings";
    private const double SimilarityThreshold   = 0.35;
    private const int    MaxTagsToMatch        = 4;

    public string Name => "InstructionAgent";

    public InstructionAgent(
        IOllamaClient ollama,
        IVectorRepository vectors,
        ITagVocabularyCache tagCache,
        IFeatureFlagRepository flags,
        IOptions<AssistantOptions> options,
        ILogger<InstructionAgent> logger)
    {
        _ollama   = ollama;
        _vectors  = vectors;
        _tagCache = tagCache;
        _flags    = flags;
        _options  = options.Value;
        _logger   = logger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            if (!await _flags.IsEnabledAsync("instruction-embeddings", context.CancellationToken))
            {
                _logger.LogDebug("InstructionAgent skipped — instruction-embeddings feature flag is disabled");
                return new AgentResult { Success = true };
            }

            // Step 1 — embed the query (used for both tag matching and Qdrant search)
            var queryText = BuildQueryText(context);
            var queryVec  = await _ollama.EmbedAsync(_options.EmbeddingModel, queryText, context.CancellationToken);

            // Step 2 — fetch vocabulary from cache (0 ms when warm)
            var vocabulary = await _tagCache.GetAsync(context.CancellationToken);

            if (vocabulary.Count == 0)
            {
                // No tags in DB yet — fall back to unfiltered search
                _logger.LogWarning("InstructionAgent: tag vocabulary is empty, running unfiltered search");
                var fallback = await _vectors.SearchAsync(
                    InstructionCollection, queryVec, _options.InstructionTopK,
                    null, context.CancellationToken);
                context.InstructionRules = fallback
                    .Where(r => !string.IsNullOrWhiteSpace(r.Content))
                    .Select(r => r.Content)
                    .ToList();
                return new AgentResult { Success = true };
            }

            // Step 3 — cosine similarity between query and each tag embedding
            var matched = vocabulary
                .Select(entry => (entry, score: CosineSimilarity(queryVec, entry.Embedding)))
                .Where(x => x.score >= SimilarityThreshold)
                .OrderByDescending(x => x.score)
                .Take(MaxTagsToMatch)
                .ToList();

            _logger.LogInformation(
                "InstructionAgent: matched {Count} tags for query [{Query}]: {Tags}",
                matched.Count, queryText[..Math.Min(queryText.Length, 60)],
                string.Join(", ", matched.Select(x => $"{x.entry.Language}/{x.entry.Category}:{x.score:F2}")));

            IReadOnlyList<VectorSearchResult> results;

            if (matched.Count == 0)
            {
                // Nothing matched — search without filter
                _logger.LogDebug("InstructionAgent: no tags matched threshold, running unfiltered search");
                results = await _vectors.SearchAsync(
                    InstructionCollection, queryVec, _options.InstructionTopK,
                    null, context.CancellationToken);
            }
            else
            {
                // Step 4 — run one Qdrant search per matched tag concurrently, merge results
                var perTagK  = Math.Max(2, _options.InstructionTopK / matched.Count);
                var searches = matched.Select(x => _vectors.SearchAsync(
                    InstructionCollection, queryVec, perTagK,
                    new Dictionary<string, string>
                    {
                        ["language"] = x.entry.Language,
                        ["category"] = x.entry.Category,
                    },
                    context.CancellationToken));

                var allResults = await Task.WhenAll(searches);

                // Merge and deduplicate by content, highest score first
                var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var merged = new List<VectorSearchResult>();
                foreach (var r in allResults.SelectMany(rs => rs).OrderByDescending(r => r.Score))
                {
                    if (!string.IsNullOrWhiteSpace(r.Content) && seen.Add(r.Content))
                        merged.Add(r);
                    if (merged.Count >= _options.InstructionTopK) break;
                }
                results = merged;
            }

            context.InstructionRules = results
                .Where(r => !string.IsNullOrWhiteSpace(r.Content))
                .Select(r => r.Content)
                .ToList();

            _logger.LogInformation(
                "InstructionAgent retrieved {Count} rules for conversation {ConversationId}",
                context.InstructionRules.Count, context.ConversationId);

            context.PublishEvent(new InstructionsRetrievedEvent(
                Name, DateTimeOffset.UtcNow,
                context.InstructionRules.Count,
                matched.Select(x => $"{x.entry.Language}/{x.entry.Category}").ToList()));

            return new AgentResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InstructionAgent failed — continuing without instructions");
            return new AgentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Builds the semantic search text from the user message and detected intent.
    /// More specific than just the user message — guides both tag matching and Qdrant search.
    /// </summary>
    private static string BuildQueryText(AgentContext context)
    {
        var intentHint = context.Intent switch
        {
            AgentIntent.CodeGeneration     => "coding standards architecture patterns",
            AgentIntent.CodeReview         => "code review security quality change risk",
            AgentIntent.UnitTest           => "unit testing mocking arrange act assert",
            AgentIntent.Documentation      => "documentation comments api standards",
            AgentIntent.CodeExplanation    => "code explanation architecture conventions",
            AgentIntent.RepositoryQuestion => "codebase architecture structure conventions",
            _                              => "engineering standards best practices"
        };
        return $"{context.UserMessage} {intentHint}";
    }

    /// <summary>
    /// Computes cosine similarity between two float vectors.
    /// Returns a value in [-1, 1]; threshold is applied by the caller.
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }
}
