namespace AssistantApi.Core.Interfaces;

/// <summary>
/// Represents a registered agent and the condition under which it should run.
/// </summary>
public sealed class AgentRegistration
{
    /// <summary>Unique name used to identify the agent in routing and logging.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description used by the LLM router to pick agents.</summary>
    public required string Description { get; init; }

    /// <summary>Returns true when this agent should be invoked for the given context.</summary>
    public required Func<AgentContext, bool> Condition { get; init; }

    /// <summary>Creates the agent instance from the DI container.</summary>
    public required Func<IServiceProvider, IAgent> Factory { get; init; }
}

/// <summary>
/// Holds all registered agents. Resolved once per pipeline invocation.
/// Agents are invoked in registration order unless the router overrides the sequence.
/// </summary>
public interface IAgentRegistry
{
    IReadOnlyList<AgentRegistration> Registrations { get; }
    void Register(AgentRegistration registration);
}
