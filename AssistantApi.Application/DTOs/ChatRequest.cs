using AssistantApi.Core.Interfaces;

namespace AssistantApi.Application.DTOs;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string? RepositoryFilter { get; set; }
    public bool Stream { get; set; } = false;

    /// <summary>
    /// When set, bypasses the agent's prompt-building pipeline and sends these messages
    /// directly to the LLM. Used for pass-through requests from Continue.
    /// </summary>
    public List<ChatMessage>? MessagesOverride { get; set; }
}

public class ChatResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public List<SourceReference> Sources { get; set; } = [];
}

public class SourceReference
{
    public string FilePath { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public double Score { get; set; }
}
