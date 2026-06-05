using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Retrieves coding standards from instruction-embeddings using semantic search.
/// Detects the programming language from the user's message and retrieved code chunks,
/// then applies a Qdrant metadata filter so only instructions for that language are returned.
///
/// This prevents Python rules from being injected into a C# response and vice versa,
/// even when many instruction files are uploaded across multiple languages.
/// </summary>
public class InstructionAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly AssistantOptions _options;
    private readonly ILogger<InstructionAgent> _logger;

    // Maps detected language names to the tag used in instruction file front matter
    private static readonly Dictionary<string, string> LanguageTagMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"]     = "csharp",
        ["c#"]         = "csharp",
        [".net"]       = "csharp",
        ["dotnet"]     = "csharp",
        ["typescript"] = "typescript",
        ["ts"]         = "typescript",
        ["tsx"]        = "typescript",
        ["javascript"] = "javascript",
        ["js"]         = "javascript",
        ["jsx"]        = "javascript",
        ["react"]      = "typescript",
        ["angular"]    = "typescript",
        ["vue"]        = "javascript",
        ["python"]     = "python",
        ["py"]         = "python",
        ["go"]         = "go",
        ["golang"]     = "go",
        ["java"]       = "java",
        ["rust"]       = "rust",
        ["ruby"]       = "ruby",
    };

    public string Name => "InstructionAgent";

    public InstructionAgent(
        IOllamaClient ollama,
        IVectorRepository vectors,
        IOptions<AssistantOptions> options,
        ILogger<InstructionAgent> logger)
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
            var query = BuildInstructionQuery(context);

            var vector = await _ollama.EmbedAsync(_options.EmbeddingModel, query, context.CancellationToken);

            // Detect language from context and apply metadata filter
            var languageTag = DetectLanguageTag(context);
            Dictionary<string, string>? filters = null;

            if (languageTag is not null)
            {
                filters = new Dictionary<string, string> { ["language"] = languageTag };
                _logger.LogDebug("InstructionAgent filtering by language={Language}", languageTag);
            }

            var results = await _vectors.SearchAsync(
                "instruction-embeddings", vector, _options.InstructionTopK,
                filters,
                context.CancellationToken);

            // If language-filtered search returns nothing, fall back to unfiltered
            if (results.Count == 0 && filters is not null)
            {
                _logger.LogDebug("No language-filtered instructions found, falling back to unfiltered search");
                results = await _vectors.SearchAsync(
                    "instruction-embeddings", vector, _options.InstructionTopK,
                    null,
                    context.CancellationToken);
            }

            context.InstructionRules = results
                .Select(r => r.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            _logger.LogInformation(
                "InstructionAgent retrieved {Count} rules (language={Language}) for conversation {ConversationId}",
                context.InstructionRules.Count, languageTag ?? "any", context.ConversationId);

            return new AgentResult { Success = true, Response = string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InstructionAgent failed — continuing without instructions");
            return new AgentResult { Success = false, Response = string.Empty, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Detects the programming language from the user's message keywords first,
    /// then falls back to the dominant language in retrieved code chunks.
    /// Returns the normalised language tag used in instruction file front matter,
    /// or null if no language can be detected.
    /// </summary>
    private static string? DetectLanguageTag(AgentContext context)
    {
        var lower = context.UserMessage.ToLowerInvariant();

        // Check user message for explicit language mentions
        foreach (var (keyword, tag) in LanguageTagMap)
        {
            if (lower.Contains(keyword))
                return tag;
        }

        // Fall back to dominant language from retrieved code chunks
        var chunkLanguage = context.RetrievedChunks
            .Select(c => c.Language)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        if (chunkLanguage is not null &&
            LanguageTagMap.TryGetValue(chunkLanguage, out var mappedTag))
            return mappedTag;

        return null;
    }

    private static string BuildInstructionQuery(AgentContext context) =>
        context.Intent switch
        {
            AgentIntent.CodeGeneration   => "coding standards rules for generating code",
            AgentIntent.CodeReview       => "code review rules quality standards forbidden patterns",
            AgentIntent.UnitTest         => "unit testing standards test naming conventions mocking rules",
            AgentIntent.Documentation    => "documentation standards comment conventions",
            AgentIntent.CodeExplanation  => "code explanation architecture patterns",
            _                            => $"general coding standards best practices {context.Intent}"
        };
}
