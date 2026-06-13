using System.Text.Json.Serialization;

namespace AssistantApi.Application.DTOs;

// ── Tool definitions (inbound from Copilot) ──────────────────────────────────

public class OpenAiTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAiToolFunction Function { get; set; } = new();
}

public class OpenAiToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}

// ── Tool calls (outbound to Copilot) ─────────────────────────────────────────

public class OpenAiToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = $"call_{Guid.NewGuid():N}";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAiToolCallFunction Function { get; set; } = new();
}

public class OpenAiToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "{}";
}

// ── Tool result message (inbound from Copilot after tool execution) ───────────

public class OpenAiToolResultMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "tool";

    [JsonPropertyName("tool_call_id")]
    public string ToolCallId { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

// ── Parsed file edit from LLM response ───────────────────────────────────────

public class ParsedFileEdit
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsNew { get; set; } = true;
}
