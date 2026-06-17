using AssistantApi.Core.Interfaces;

namespace AssistantApi.Application.Agents;

/// <summary>
/// In-memory singleton registry. Registrations are added once at startup from Program.cs.
/// Thread-safe for reads after startup; Register() is only called during host configuration.
/// </summary>
public sealed class AgentRegistry : IAgentRegistry
{
    private readonly List<AgentRegistration> _registrations = [];

    public IReadOnlyList<AgentRegistration> Registrations => _registrations;

    public void Register(AgentRegistration registration) => _registrations.Add(registration);
}
