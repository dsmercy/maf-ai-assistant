using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssistantApi.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Services;

/// <summary>
/// Handles OpenAI tool-calling protocol for requests that include a tools[] array.
///
/// Two callers:
///   1. Our VS Code extension — sends create_new_file / edit_existing_file schemas.
///      The LLM returns tool_calls[]; the extension executes them via vscode.workspace.fs.
///   2. GitHub Copilot custom model — sends its own tool list. We handle the two-turn protocol.
///
/// First turn:  LLM returns tool_calls[] with finish_reason="tool_calls"
/// Second turn: Messages contain role="tool" results → return plain confirmation
/// </summary>
public class ToolCallService
{
    // Fallback: parse ### File: blocks from models that don't support native tool-calling
    private static readonly Regex FileBlockRegex = new(
        @"###\s*File:\s*(.+?)\r?\n```[\w]*\r?\n([\s\S]*?)```",
        RegexOptions.Compiled);

    private readonly ChatService _chatService;
    private readonly ILogger<ToolCallService> _logger;

    public ToolCallService(ChatService chatService, ILogger<ToolCallService> logger)
    {
        _chatService = chatService;
        _logger      = logger;
    }

    public async Task<OpenAiChatResponse> HandleAsync(
        OpenAiChatRequest request,
        string userId,
        string conversationId,
        CancellationToken ct)
    {
        if (IsToolResultTurn(request.Messages))
            return await HandleToolResultTurnAsync(request, userId, conversationId, ct);

        return await HandleFirstTurnAsync(request, userId, conversationId, ct);
    }

    // ── First turn ────────────────────────────────────────────────────────────

    private async Task<OpenAiChatResponse> HandleFirstTurnAsync(
        OpenAiChatRequest request,
        string userId,
        string conversationId,
        CancellationToken ct)
    {
        var userMessage = request.Messages
            .LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        var augmented = BuildFileAwarePrompt(userMessage);

        var chatRequest = new ChatRequest
        {
            Message        = augmented,
            ConversationId = conversationId,
            Stream         = false,
        };

        var result = await _chatService.HandleAsync(chatRequest, userId, ct);

        // Synthesise tool_calls from ### File: blocks in the response
        var fileBlocks = ParseFileBlocks(result.Response);
        if (fileBlocks.Count > 0)
        {
            _logger.LogInformation("ToolCallService: synthesised {Count} tool call(s) from ### File: blocks", fileBlocks.Count);
            var synthesised = fileBlocks.Select(b => new OpenAiToolCall
            {
                Id = $"call_{Guid.NewGuid():N}",
                Function = new OpenAiToolCallFunction
                {
                    Name      = b.IsNew ? ToolNames.CreateNewFile : ToolNames.EditExistingFile,
                    Arguments = JsonSerializer.Serialize(new
                    {
                        filepath = b.FilePath,
                        contents = b.Content,
                    }),
                }
            }).ToList();
            return BuildToolCallsResponse(request.Model, synthesised);
        }

        // No file operations — plain message
        _logger.LogInformation("ToolCallService: no file operations, returning plain message");
        return BuildPlainResponse(request.Model, result.Response);
    }

    // ── Tool-result turn ──────────────────────────────────────────────────────

    private async Task<OpenAiChatResponse> HandleToolResultTurnAsync(
        OpenAiChatRequest request,
        string userId,
        string conversationId,
        CancellationToken ct)
    {
        var toolResults = request.Messages
            .Where(m => m.Role == "tool")
            .Select(m => m.Content)
            .ToList();

        var summary = toolResults.Count > 0
            ? $"The following files were created/edited successfully:\n{string.Join("\n", toolResults)}\n\nConfirm in one concise sentence."
            : "The requested file operations completed. Confirm in one concise sentence.";

        var chatRequest = new ChatRequest
        {
            Message        = summary,
            ConversationId = conversationId,
            Stream         = false,
        };

        var result = await _chatService.HandleAsync(chatRequest, userId, ct);
        return BuildPlainResponse(request.Model, result.Response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsToolResultTurn(List<OpenAiMessage> messages) =>
        messages.Any(m => m.Role == "tool");

    private static string BuildFileAwarePrompt(string userMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine(userMessage);
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: For every file you need to create or modify, use the create_new_file or edit_existing_file tool.");
        sb.AppendLine("Provide the complete file contents — do not truncate or abbreviate.");
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
                IsNew    = true,
            });
        }
        return results;
    }

    private static OpenAiChatResponse BuildToolCallsResponse(string model, List<OpenAiToolCall> toolCalls) =>
        new()
        {
            Model = model,
            Choices =
            [
                new OpenAiChoice
                {
                    Index        = 0,
                    Message      = new OpenAiMessage
                    {
                        Role      = "assistant",
                        Content   = null,
                        ToolCalls = toolCalls,
                    },
                    FinishReason = "tool_calls",
                }
            ]
        };

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
                    FinishReason = "stop",
                }
            ]
        };
}
