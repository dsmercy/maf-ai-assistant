namespace AssistantApi.Core.Entities;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ConversationMessage> Messages { get; set; } = [];
}

public class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty; // user | assistant
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AgentIntent? DetectedIntent { get; set; }
    public long? LatencyMs { get; set; }
}

public enum AgentIntent
{
    CodeGeneration,
    CodeExplanation,
    CodeReview,
    UnitTest,
    Documentation,
    RepositoryQuestion,
    GeneralQuestion
}
