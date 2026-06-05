namespace AssistantApi.Core.Entities;

/// <summary>
/// Represents a chat conversation between a user and the AI assistant.
/// A conversation groups multiple messages together under a shared ID,
/// enabling multi-turn context and persistent chat history.
/// </summary>
public class Conversation
{
    /// <summary>Unique identifier for the conversation. Typically derived from the client-supplied conversationId.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identity of the user who owns this conversation (email or sub claim from JWT).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the conversation was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent message. Updated on every new message.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Ordered list of all messages in this conversation.</summary>
    public List<ConversationMessage> Messages { get; set; } = [];
}

/// <summary>
/// Represents a single message within a conversation — either from the user or the assistant.
/// Stores the content, role, detected intent, and response latency for observability.
/// </summary>
public class ConversationMessage
{
    /// <summary>Unique identifier for this message.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The conversation this message belongs to.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Who sent this message — "user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The full text content of the message.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this message was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The intent classified for this exchange.
    /// Set on assistant messages to record what the agent pipeline understood.
    /// </summary>
    public AgentIntent? DetectedIntent { get; set; }

    /// <summary>
    /// Time in milliseconds from receiving the user message to returning the assistant response.
    /// Only set on assistant messages.
    /// </summary>
    public long? LatencyMs { get; set; }
}

/// <summary>
/// Represents the type of task the user is asking the agent to perform.
/// Used by the OrchestratorAgent to decide which agents to invoke and
/// which prompt template to load from the database.
/// </summary>
public enum AgentIntent
{
    /// <summary>User wants new code written (generate, create, implement).</summary>
    CodeGeneration,
    /// <summary>User wants existing code explained (explain, what does, how does).</summary>
    CodeExplanation,
    /// <summary>User wants code reviewed or improved (review, refactor, fix).</summary>
    CodeReview,
    /// <summary>User wants unit tests generated (unit test, xunit, nunit).</summary>
    UnitTest,
    /// <summary>User wants documentation written (document, xml doc, readme).</summary>
    Documentation,
    /// <summary>User is asking a question about the indexed codebase (file, class, namespace).</summary>
    RepositoryQuestion,
    /// <summary>General software question not directly tied to the codebase.</summary>
    GeneralQuestion
}
