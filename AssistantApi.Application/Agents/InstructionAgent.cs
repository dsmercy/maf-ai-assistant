using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Retrieves relevant coding standards and rules from the instruction-embeddings Qdrant collection.
/// Always runs before any code generation to ensure the LLM follows team conventions.
///
/// Process:
///   1. Build a task-specific search query based on the detected intent
///      (e.g. "coding standards for code generation" vs "unit testing conventions")
///   2. Embed the query via Ollama nomic-embed-text
///   3. Search instruction-embeddings for the top 10 most relevant rules
///   4. Populate AgentContext.InstructionRules for CodingAgent to inject into the system prompt
///
/// If no instruction files have been uploaded, the collection is empty and
/// this step returns nothing — the LLM falls back to general best practices.
/// </summary>
public class InstructionAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IVectorRepository _vectors;
    private readonly AssistantOptions _options;
    private readonly ILogger<InstructionAgent> _logger;

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

    /// <summary>
    /// Searches instruction-embeddings for rules relevant to the current task intent
    /// and writes them into context.InstructionRules.
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            var query = BuildInstructionQuery(context);

            var vector = await _ollama.EmbedAsync(_options.EmbeddingModel, query, context.CancellationToken);

            var results = await _vectors.SearchAsync(
                "instruction-embeddings", vector, topK: 10,
                filters: null,
                ct: context.CancellationToken);

            context.InstructionRules = results
                .Select(r => r.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            _logger.LogInformation("InstructionAgent retrieved {Count} rules for conversation {ConversationId}",
                context.InstructionRules.Count, context.ConversationId);

            return new AgentResult { Success = true, Response = string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InstructionAgent failed — continuing without instructions");
            return new AgentResult { Success = false, Response = string.Empty, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Builds a task-specific search query so the most relevant coding rules
    /// are retrieved for the current intent type.
    /// </summary>
    private static string BuildInstructionQuery(AgentContext context) =>
        context.Intent switch
        {
            AgentIntent.CodeGeneration => "coding standards rules for generating code",
            AgentIntent.CodeReview => "code review rules quality standards forbidden patterns",
            AgentIntent.UnitTest => "unit testing standards test naming conventions mocking rules",
            AgentIntent.Documentation => "documentation standards xml doc comment conventions",
            _ => $"general coding standards best practices {context.Intent}"
        };
}
