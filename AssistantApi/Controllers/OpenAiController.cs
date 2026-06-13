using System.Text.Json;
using AssistantApi.Application.DTOs;
using AssistantApi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

/// <summary>
/// OpenAI-compatible and Anthropic-compatible API surface.
/// Consumed by Open WebUI, GitHub Copilot custom models, and the custom VS Code extension.
/// All endpoints delegate to ChatService and route through the full RAG agent pipeline.
///
/// Endpoints:
///   GET  /v1/models                  — model discovery
///   GET  /v1/models/{id}             — single model lookup
///   POST /v1/chat/completions        — OpenAI Chat Completions API
///   POST /v1/responses               — OpenAI Responses API (Copilot requirement)
///   POST /v1/messages                — Anthropic Messages API (Copilot requirement)
/// </summary>
[ApiController]
[Route("v1")]
public class OpenAiController : ControllerBase
{
    private static readonly string[] KnownModelIds =
    [
        "ai-assistant",
        "assistant-14b",
        "assistant-30b",
    ];

    private readonly ChatService _chatService;
    private readonly ToolCallService _toolCallService;
    private readonly ILogger<OpenAiController> _logger;

    public OpenAiController(
        ChatService chatService,
        ToolCallService toolCallService,
        ILogger<OpenAiController> logger)
    {
        _chatService     = chatService;
        _toolCallService = toolCallService;
        _logger          = logger;
    }

    // ── Model discovery ───────────────────────────────────────────────────────

    /// <summary>GET /v1/models</summary>
    [HttpGet("models")]
    [AllowAnonymous]
    public ActionResult<OpenAiModelsResponse> GetModels() =>
        Ok(new OpenAiModelsResponse
        {
            Data = KnownModelIds
                .Select(id => new OpenAiModel { Id = id, OwnedBy = "ai-coding-assistant" })
                .ToList()
        });

    /// <summary>GET /v1/models/{modelId} — single model lookup required by Cline and Copilot.</summary>
    [HttpGet("models/{modelId}")]
    [AllowAnonymous]
    public ActionResult<OpenAiModel> GetModel(string modelId) =>
        Ok(new OpenAiModel { Id = modelId, OwnedBy = "ai-coding-assistant" });

    // ── Chat Completions API ──────────────────────────────────────────────────

    /// <summary>
    /// POST /v1/chat/completions — OpenAI Chat Completions format.
    /// When tools[] is present (e.g. from GitHub Copilot), routes through ToolCallService
    /// which converts LLM ### File: blocks into tool_call responses Copilot can execute.
    /// </summary>
    [HttpPost("chat/completions")]
    public async Task ChatCompletions([FromBody] OpenAiChatRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";

        // Find the last user message — content may be null on tool-calls turns
        var lastUser = request.Messages.LastOrDefault(m => m.Role == "user");
        var userContent = lastUser?.Content
                          ?? request.Messages.LastOrDefault(m => m.Role == "tool")?.Content
                          ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userContent) && request.Tools is not { Count: > 0 })
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "No user message found." }, ct);
            return;
        }

        var conversationId = Request.Headers["X-Conversation-Id"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString();

        // Tool-calling path: Copilot sent tools[] — use ToolCallService
        if (request.Tools is { Count: > 0 })
        {
            _logger.LogInformation("Tool-calling request with {Count} tool(s) for conversation {Id}",
                request.Tools.Count, conversationId);
            try
            {
                var toolResponse = await _toolCallService.HandleAsync(request, userId, conversationId, ct);
                Response.ContentType = "application/json";
                await Response.WriteAsJsonAsync(toolResponse, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ToolCallService failed, falling back to plain response");
                // Fallback: return a plain chat completion so Copilot doesn't see "no choices"
                var fallbackRequest = new ChatRequest
                {
                    Message        = userContent,
                    ConversationId = conversationId,
                    Stream         = false
                };
                await HandleChatCompletionsAsync(fallbackRequest, userId, request.Model, ct);
            }
            return;
        }

        // Normal path
        var chatRequest = new ChatRequest
        {
            Message        = userContent,
            ConversationId = conversationId,
            Stream         = request.Stream
        };

        if (request.Stream)
            await HandleChatCompletionsStreamAsync(chatRequest, userId, request.Model, ct);
        else
            await HandleChatCompletionsAsync(chatRequest, userId, request.Model, ct);
    }

    // ── Responses API ─────────────────────────────────────────────────────────

    /// <summary>
    /// POST /v1/responses — OpenAI Responses API format required by GitHub Copilot custom models.
    /// Input may be a plain string or an array of {role, content} message objects.
    /// </summary>
    [HttpPost("responses")]
    public async Task Responses([FromBody] OpenAiResponsesRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";

        string userMessage;
        if (request.Input.ValueKind == JsonValueKind.String)
        {
            userMessage = request.Input.GetString() ?? string.Empty;
        }
        else if (request.Input.ValueKind == JsonValueKind.Array)
        {
            var last = request.Input.EnumerateArray()
                .LastOrDefault(e =>
                    e.TryGetProperty("role", out var r) && r.GetString() == "user");
            userMessage = last.ValueKind != JsonValueKind.Undefined
                ? last.GetProperty("content").GetString() ?? string.Empty
                : string.Empty;
        }
        else
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "Unsupported input format." }, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.Instructions))
            userMessage = $"{request.Instructions}\n\n{userMessage}";

        var conversationId = Request.Headers["X-Conversation-Id"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString();

        var chatRequest = new ChatRequest
        {
            Message = userMessage,
            ConversationId = conversationId,
            Stream = request.Stream
        };

        if (request.Stream)
            await HandleResponsesStreamAsync(chatRequest, userId, request.Model, ct);
        else
        {
            var result = await _chatService.HandleAsync(chatRequest, userId, ct);
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new OpenAiResponsesResponse
            {
                Model = request.Model,
                Output =
                [
                    new OpenAiResponseOutput
                    {
                        Content = [new OpenAiResponseContent { Text = result.Response }]
                    }
                ]
            }, ct);
        }
    }

    // ── Messages API ──────────────────────────────────────────────────────────

    /// <summary>
    /// POST /v1/messages — Anthropic Messages API format required by GitHub Copilot custom models.
    /// Maps the last user message in the thread to ChatService.
    /// </summary>
    [HttpPost("messages")]
    public async Task Messages([FromBody] AnthropicMessagesRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";

        var lastUser = request.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUser is null)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "No user message found." }, ct);
            return;
        }

        var userMessage = lastUser.Content;
        if (!string.IsNullOrWhiteSpace(request.System))
            userMessage = $"{request.System}\n\n{userMessage}";

        var conversationId = Request.Headers["X-Conversation-Id"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString();

        var chatRequest = new ChatRequest
        {
            Message = userMessage,
            ConversationId = conversationId,
            Stream = request.Stream
        };

        if (request.Stream)
            await HandleMessagesStreamAsync(chatRequest, userId, request.Model, ct);
        else
        {
            var result = await _chatService.HandleAsync(chatRequest, userId, ct);
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new AnthropicMessagesResponse
            {
                Model = request.Model,
                Content = [new AnthropicContent { Text = result.Response }]
            }, ct);
        }
    }

    // ── Streaming helpers ─────────────────────────────────────────────────────

    private async Task HandleChatCompletionsAsync(
        ChatRequest request, string userId, string model, CancellationToken ct)
    {
        var result = await _chatService.HandleAsync(request, userId, ct);
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(new OpenAiChatResponse
        {
            Model = model,
            Choices =
            [
                new OpenAiChoice
                {
                    Index = 0,
                    Message = new OpenAiMessage { Role = "assistant", Content = result.Response },
                    FinishReason = "stop"
                }
            ]
        }, ct);
    }

    private async Task HandleChatCompletionsStreamAsync(
        ChatRequest request, string userId, string model, CancellationToken ct)
    {
        SetSseHeaders();
        var chunkId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await WriteSseAsync(JsonSerializer.Serialize(new OpenAiStreamChunk
        {
            Id = chunkId, Created = created, Model = model,
            Choices = [new() { Index = 0, Delta = new() { Role = "assistant" } }]
        }), ct);

        try
        {
            await foreach (var token in _chatService.StreamAsync(request, userId, ct))
                await WriteSseAsync(JsonSerializer.Serialize(new OpenAiStreamChunk
                {
                    Id = chunkId, Created = created, Model = model,
                    Choices = [new() { Index = 0, Delta = new() { Content = token } }]
                }), ct);
        }
        catch (OperationCanceledException) { }

        await WriteSseAsync(JsonSerializer.Serialize(new OpenAiStreamChunk
        {
            Id = chunkId, Created = created, Model = model,
            Choices = [new() { Index = 0, Delta = new(), FinishReason = "stop" }]
        }), ct);

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    // Responses API streaming — uses server-sent events with response.* event types
    private async Task HandleResponsesStreamAsync(
        ChatRequest request, string userId, string model, CancellationToken ct)
    {
        SetSseHeaders();
        var responseId = $"resp-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // response.created
        await WriteSseEventAsync("response.created", JsonSerializer.Serialize(new
        {
            type = "response.created",
            response = new { id = responseId, @object = "response", created_at = created, model, status = "in_progress" }
        }), ct);

        // response.output_item.added
        await WriteSseEventAsync("response.output_item.added", JsonSerializer.Serialize(new
        {
            type = "response.output_item.added",
            output_index = 0,
            item = new { type = "message", role = "assistant", content = Array.Empty<object>() }
        }), ct);

        try
        {
            int i = 0;
            await foreach (var token in _chatService.StreamAsync(request, userId, ct))
            {
                await WriteSseEventAsync("response.output_text.delta", JsonSerializer.Serialize(new
                {
                    type = "response.output_text.delta",
                    output_index = 0,
                    content_index = 0,
                    delta = token
                }), ct);
                i++;
            }
        }
        catch (OperationCanceledException) { }

        // response.completed
        await WriteSseEventAsync("response.completed", JsonSerializer.Serialize(new
        {
            type = "response.completed",
            response = new { id = responseId, @object = "response", created_at = created, model, status = "completed" }
        }), ct);

        await Response.Body.FlushAsync(ct);
    }

    // Messages API streaming — uses Anthropic SSE event types
    private async Task HandleMessagesStreamAsync(
        ChatRequest request, string userId, string model, CancellationToken ct)
    {
        SetSseHeaders();
        var messageId = $"msg_{Guid.NewGuid():N}";

        await WriteSseEventAsync("message_start", JsonSerializer.Serialize(new
        {
            type = "message_start",
            message = new { id = messageId, type = "message", role = "assistant", model, content = Array.Empty<object>(), stop_reason = (string?)null }
        }), ct);

        await WriteSseEventAsync("content_block_start", JsonSerializer.Serialize(new
        {
            type = "content_block_start",
            index = 0,
            content_block = new { type = "text", text = "" }
        }), ct);

        try
        {
            await foreach (var token in _chatService.StreamAsync(request, userId, ct))
                await WriteSseEventAsync("content_block_delta", JsonSerializer.Serialize(new
                {
                    type = "content_block_delta",
                    index = 0,
                    delta = new { type = "text_delta", text = token }
                }), ct);
        }
        catch (OperationCanceledException) { }

        await WriteSseEventAsync("content_block_stop", JsonSerializer.Serialize(new { type = "content_block_stop", index = 0 }), ct);
        await WriteSseEventAsync("message_delta", JsonSerializer.Serialize(new
        {
            type = "message_delta",
            delta = new { stop_reason = "end_turn", stop_sequence = (string?)null }
        }), ct);
        await WriteSseEventAsync("message_stop", JsonSerializer.Serialize(new { type = "message_stop" }), ct);

        await Response.Body.FlushAsync(ct);
    }

    private void SetSseHeaders()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");
    }

    private async Task WriteSseAsync(string json, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteSseEventAsync(string eventName, string json, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
