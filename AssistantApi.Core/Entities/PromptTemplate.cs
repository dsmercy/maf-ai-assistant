namespace AssistantApi.Core.Entities;

public class PromptTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;  // matches AgentIntent enum value
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
