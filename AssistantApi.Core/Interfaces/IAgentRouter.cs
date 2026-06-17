namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Determines the ordered list of agent names to invoke for a given context.
/// OrchestratorAgent calls RouteAsync() once per request and runs only the returned agents.
/// </summary>
public interface IAgentRouter
{
    /// <summary>
    /// Returns agent names in the order they should execute.
    /// Names must match AgentRegistration.Name values in IAgentRegistry.
    /// </summary>
    Task<IReadOnlyList<string>> RouteAsync(AgentContext context, IReadOnlyList<string> availableAgents, CancellationToken ct = default);
}
