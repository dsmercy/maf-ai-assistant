using System.Text.Json;
using AssistantApi.Application.DTOs;
using AssistantApi.Application.Services;
using AssistantApi.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

/// <summary>
/// OpenAI-compatible API surface consumed by Open WebUI.
/// Routes 'ai-assistant' model requests through the full RAG agent pipeline.
/// All other model names are rejected with 404 (Open WebUI falls back to Ollama directly).
/// </summary>
[ApiController]
[Route("v1")]
public class OpenAiController : ControllerBase
{
    private const string AssistantModelId = "ai-assistant";

    private readonly ChatService _chatService;
    private readonly ILogger<OpenAiController> _logger;

    public OpenAiController(ChatService chatService, ILogger<OpenAiController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>GET /v1/models — returns only ai-assistant. Ollama models come from the Ollama connection directly.</summary>
    [HttpGet("models")]
    [AllowAnonymous]
    public ActionResult<OpenAiModelsResponse> GetModels()
    {
        return Ok(new OpenAiModelsResponse
        {
            Data =
            [
                new OpenAiModel
                {
                    Id = AssistantModelId,
                    OwnedBy = "ai-coding-assistant"
                }
            ]
        });
    }

    /// <summary>POST /v1/chat/completions — OpenAI-compatible chat endpoint.</summary>
    [HttpPost("chat/completions")]
    public async Task ChatCompletions([FromBody] OpenAiChatRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";

        // Extract last user message as the prompt
        var lastUser = request.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUser is null)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "No user message found." }, ct);
            return;
        }

        // Use conversation ID from a custom header if Open WebUI sends one, else generate one
        var conversationId = Request.Headers["X-Conversation-Id"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString();

        var chatRequest = new ChatRequest
        {
            Message = lastUser.Content,
            ConversationId = conversationId,
            Stream = request.Stream
        };

        if (request.Stream)
            await HandleStreamingAsync(chatRequest, userId, request.Model, ct);
        else
            await HandleNonStreamingAsync(chatRequest, userId, request.Model, ct);
    }

    private async Task HandleNonStreamingAsync(
        ChatRequest request, string userId, string model, CancellationToken ct)
    {
        var result = await _chatService.HandleAsync(request, userId, ct);

        Response.ContentType = "application/json";
        var response = new OpenAiChatResponse
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
        };
        await Response.WriteAsJsonAsync(response, ct);
    }

    private async Task HandleStreamingAsync(
        ChatRequest request, string userId, string model, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var chunkId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Opening chunk with role
        await WriteStreamChunkAsync(new OpenAiStreamChunk
        {
            Id = chunkId,
            Created = created,
            Model = model,
            Choices = [new() { Index = 0, Delta = new() { Role = "assistant" } }]
        }, ct);

        try
        {
            await foreach (var token in _chatService.StreamAsync(request, userId, ct))
            {
                await WriteStreamChunkAsync(new OpenAiStreamChunk
                {
                    Id = chunkId,
                    Created = created,
                    Model = model,
                    Choices = [new() { Index = 0, Delta = new() { Content = token } }]
                }, ct);
            }
        }
        catch (OperationCanceledException) { }

        // Closing chunk
        await WriteStreamChunkAsync(new OpenAiStreamChunk
        {
            Id = chunkId,
            Created = created,
            Model = model,
            Choices = [new() { Index = 0, Delta = new(), FinishReason = "stop" }]
        }, ct);

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteStreamChunkAsync(OpenAiStreamChunk chunk, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(chunk);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

}
