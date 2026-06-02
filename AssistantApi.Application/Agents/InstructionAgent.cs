using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Stub: full instruction retrieval implemented in Phase 4.
/// </summary>
public class InstructionAgent : IAgent
{
    private readonly ILogger<InstructionAgent> _logger;

    public string Name => "InstructionAgent";

    public InstructionAgent(ILogger<InstructionAgent> logger)
    {
        _logger = logger;
    }

    public Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        _logger.LogDebug("InstructionAgent stub invoked for conversation {ConversationId}", context.ConversationId);
        // Phase 4: search instruction-embeddings, populate context.InstructionRules
        return Task.FromResult(new AgentResult { Success = true, Response = string.Empty });
    }
}
