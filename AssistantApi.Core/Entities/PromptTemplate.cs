namespace AssistantApi.Core.Entities;

/// <summary>
/// Stores a reusable prompt template for a specific agent intent type.
/// Templates define the system prompt and user prompt structure sent to the LLM,
/// with parameterised placeholders for instructions, code context, and the user message.
/// Templates are loaded from the database at runtime so they can be tuned without
/// redeploying the application.
/// </summary>
public class PromptTemplate
{
    /// <summary>Unique identifier for this template.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name for the template (e.g. "Code Generation", "Code Review").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The agent intent this template applies to. Maps to AgentIntent enum values
    /// (e.g. "CodeGeneration", "UnitTest", "CodeReview").
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// The system message sent to the LLM. Supports placeholders:
    /// {instructions} — coding standards retrieved by InstructionAgent,
    /// {context_chunks} — code retrieved by RepositoryAgent,
    /// {language} — dominant language detected from retrieved chunks.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// The user message template. Supports placeholders:
    /// {user_message} — the original question from the user,
    /// {language} — detected programming language.
    /// </summary>
    public string UserPromptTemplate { get; set; } = string.Empty;

    /// <summary>Whether this template is currently active. Inactive templates are ignored at runtime.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when this template was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last modification.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
