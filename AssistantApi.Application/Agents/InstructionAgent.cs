using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

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

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            // Build a task-specific query to retrieve the most relevant coding standards
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

    private static string BuildInstructionQuery(AgentContext context)
    {
        // Build a targeted query based on intent so we get the most relevant rules
        var intent = context.Intent.ToString();
        return context.Intent switch
        {
            AgentIntent.CodeGeneration => $"coding standards rules for generating {intent} code",
            AgentIntent.CodeReview => "code review rules quality standards forbidden patterns",
            AgentIntent.UnitTest => "unit testing standards test naming conventions mocking rules",
            AgentIntent.Documentation => "documentation standards xml doc comment conventions",
            _ => $"general coding standards best practices {intent}"
        };
    }
}
