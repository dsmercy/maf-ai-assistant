namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Defines the contract for all agents in the pipeline.
/// Each agent receives a shared <see cref="AgentContext"/>, performs its specific task
/// (retrieval, instruction loading, or generation), and returns an <see cref="AgentResult"/>.
/// Agents may modify the context (e.g. populating RetrievedChunks) for downstream agents.
/// </summary>
public interface IAgent
{
    /// <summary>Display name of this agent used in logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Executes the agent's task against the provided context.
    /// Agents that retrieve data (RepositoryAgent, InstructionAgent) populate
    /// the context and return an empty response. Only CodingAgent sets a non-empty response.
    /// </summary>
    /// <param name="context">Shared context object carrying the user message, retrieved chunks, and instruction rules.</param>
    /// <returns>Result containing the generated response and success status.</returns>
    Task<AgentResult> ExecuteAsync(AgentContext context);
}
