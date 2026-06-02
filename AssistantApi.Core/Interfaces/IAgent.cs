namespace AssistantApi.Core.Interfaces;

public interface IAgent
{
    string Name { get; }
    Task<AgentResult> ExecuteAsync(AgentContext context);
}
