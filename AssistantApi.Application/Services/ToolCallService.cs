using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssistantApi.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Services;

/// <summary>
/// Handles OpenAI function-calling protocol for requests that include a tools[] array.
/// Used by GitHub Copilot custom models to enable createFile / editFiles tool execution.
///
/// Flow:
///   1. Copilot sends POST /v1/chat/completions with tools[] listing available workspace tools.
///   2. ToolCallService builds a tool-aware system prompt and calls ChatService for an LLM response.
///   3. The LLM response is scanned for ### File: blocks (structured edit format).
///   4. Each block is converted to an OpenAI tool_call object (createFile or editFiles).
///   5. Response is returned with finish_reason="tool_calls" and tool_calls[].
///   6. Copilot executes the tools (writes files), then sends a second turn with tool results.
///   7. ToolCallService recognises the tool-result turn and returns a plain confirmation message.
/// </summary>
public class ToolCallService
{
    private static readonly Regex FileBlockRegex = new(
        @"###\s*File:\s*(.+?)\r?\n```[\w]*\r?\n([\s\S]*?)```",
        RegexOptions.Compiled);

    private readonly ChatService _chatService;
    private readonly ILogger<ToolCallService> _logger;

    public ToolCallService(ChatService chatService, ILogger<ToolCallService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Handles a chat/completions request that contains a tools[] array.
    /// Returns either a tool_calls response (first turn) or a plain message (tool-result turn).
    /// </summary>
    public async Task<OpenAiChatResponse> HandleAsync(
        OpenAiChatRequest request,
        string userId,
        string conversationId,
        CancellationToken ct)
    {
        // If this turn contains tool results, the LLM should produce a confirmation message.
        if (IsToolResultTurn(request.Messages))
            return await HandleToolResultTurnAsync(request, userId, conversationId, ct);

        return await HandleFirstTurnAsync(request, userId, conversationId, ct);
    }

    // ── First turn: LLM decides what files to create/edit ────────────────────

    private async Task<OpenAiChatResponse> HandleFirstTurnAsync(
        OpenAiChatRequest request,
        string userId,
        string conversationId,
        CancellationToken ct)
    {
        var toolNames = request.Tools!
            .Select(t => t.Function.Name)
            .ToList();

        // Inject tool-awareness into the user message
        var originalMessage = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        var augmentedMessage = BuildToolAwarePrompt(originalMessage, toolNames);

        var chatRequest = new ChatRequest
        {
            Message = augmentedMessage,
            ConversationId = conversationId,
            Stream = false
        };

        var result = await _chatService.HandleAsync(chatRequest, userId, ct);
        var llmText = result.Response;

        var editBlocks = ParseFileBlocks(llmText);

        if (editBlocks.Count == 0)
        {
            // LLM responded with plain text — return as normal message
            _logger.LogInformation("ToolCallService: no file blocks in response, returning plain message");
            return BuildPlainResponse(request.Model, llmText);
        }

        _logger.LogInformation("ToolCallService: found {Count} file block(s), building tool_calls", editBlocks.Count);

        var toolCalls = editBlocks
            .Select(block => BuildToolCall(block, toolNames))
            .ToList();

        return new OpenAiChatResponse
        {
            Model = request.Model,
            Choices =
            [
                new OpenAiChoice
                {
                    Index = 0,
                    Message = new OpenAiMessage
                    {
                        Role      = "assistant",
                        Content   = null!,   // must be null (not empty string) for tool_calls turn
                        ToolCalls = toolCalls
                    },
                    FinishReason = "tool_calls"
                    // ToolCalls not duplicated on choice — only on message
                }
            ]
        };
    }

    // ── Tool-result turn: tools have been executed, return confirmation ────────

    private async Task<OpenAiChatResponse> HandleToolResultTurnAsync(
        OpenAiChatRequest request,
        string userId,
        string conversationId,
        CancellationToken ct)
    {
        // Summarise what was done from the tool results
        var toolResults = request.Messages
            .Where(m => m.Role == "tool")
            .Select(m => m.Content)
            .ToList();

        var summary = toolResults.Count > 0
            ? $"The following files were created/edited successfully:\n{string.Join("\n", toolResults)}\n\nConfirm what was done in one concise sentence."
            : "The requested file operations completed. Confirm in one concise sentence.";

        var chatRequest = new ChatRequest
        {
            Message = summary,
            ConversationId = conversationId,
            Stream = false
        };

        var result = await _chatService.HandleAsync(chatRequest, userId, ct);
        return BuildPlainResponse(request.Model, result.Response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsToolResultTurn(List<OpenAiMessage> messages) =>
        messages.Any(m => m.Role == "tool");

    private static string BuildToolAwarePrompt(string userMessage, List<string> toolNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine(userMessage);
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: For every file you create or modify, output it using this exact format:");
        sb.AppendLine("### File: <relative/path/to/file.ext>");
        sb.AppendLine("```<language>");
        sb.AppendLine("<complete file content>");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Output ALL files that need to be created or modified. Do not truncate content.");
        return sb.ToString();
    }

    private static List<ParsedFileEdit> ParseFileBlocks(string text)
    {
        var results = new List<ParsedFileEdit>();
        foreach (Match match in FileBlockRegex.Matches(text))
        {
            results.Add(new ParsedFileEdit
            {
                FilePath = match.Groups[1].Value.Trim(),
                Content  = match.Groups[2].Value,
                IsNew    = true
            });
        }
        return results;
    }

    private static OpenAiToolCall BuildToolCall(ParsedFileEdit block, List<string> availableTools)
    {
        // Prefer createFile if available, else editFiles, else fallback to createFile name
        var toolName = availableTools.Contains("createFile")  ? "createFile"
                     : availableTools.Contains("editFiles")   ? "editFiles"
                     : "createFile";

        var args = toolName == "editFiles"
            ? JsonSerializer.Serialize(new
            {
                files = new[]
                {
                    new { path = block.FilePath, content = block.Content }
                }
            })
            : JsonSerializer.Serialize(new
            {
                path    = block.FilePath,
                content = block.Content
            });

        return new OpenAiToolCall
        {
            Id = $"call_{Guid.NewGuid():N}",
            Function = new OpenAiToolCallFunction
            {
                Name      = toolName,
                Arguments = args
            }
        };
    }

    private static OpenAiChatResponse BuildPlainResponse(string model, string content) =>
        new()
        {
            Model = model,
            Choices =
            [
                new OpenAiChoice
                {
                    Index        = 0,
                    Message      = new OpenAiMessage { Role = "assistant", Content = content },
                    FinishReason = "stop"
                }
            ]
        };
}
