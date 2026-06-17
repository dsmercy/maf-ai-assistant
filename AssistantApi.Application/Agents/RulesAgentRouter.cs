using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Keyword-based router extracted from the original OrchestratorAgent.
/// Always returns a valid ordered list — used as the fallback when LlmAgentRouter is
/// disabled or fails.
/// </summary>
public sealed class RulesAgentRouter : IAgentRouter
{
    public Task<IReadOnlyList<string>> RouteAsync(
        AgentContext context, IReadOnlyList<string> availableAgents, CancellationToken ct = default)
    {
        var order = new List<string>();

        if (availableAgents.Contains("InstructionAgent"))
            order.Add("InstructionAgent");

        if (RequiresRepositoryContext(context.Intent) && availableAgents.Contains("RepositoryAgent"))
            order.Add("RepositoryAgent");

        if (availableAgents.Contains("CodingAgent"))
            order.Add("CodingAgent");

        return Task.FromResult<IReadOnlyList<string>>(order);
    }

    public static bool RequiresRepositoryContext(AgentIntent intent) => intent is
        AgentIntent.CodeReview or
        AgentIntent.RepositoryQuestion or
        AgentIntent.CodeExplanation or
        AgentIntent.CodeGeneration;
}
