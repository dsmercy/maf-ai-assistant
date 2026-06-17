namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Marker interface for agents that support token-by-token streaming output.
/// OrchestratorAgent detects this interface at runtime to find the correct agent
/// to call for streaming requests — no hardcoded type references needed.
/// </summary>
public interface IStreamingAgent : IAgent
{
    IAsyncEnumerable<string> StreamAsync(AgentContext context);
}
