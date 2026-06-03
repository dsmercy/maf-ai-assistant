using AssistantApi.Application.DTOs;
using AssistantApi.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";
        var response = await _chatService.HandleAsync(request, userId, ct);
        return Ok(response);
    }

    /// <summary>
    /// Server-Sent Events streaming endpoint. Each token is delivered as:
    ///   data: {"token":"..."}
    /// Terminated by:
    ///   data: [DONE]
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "anonymous";

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var token in _chatService.StreamAsync(request, userId, ct))
            {
                var escaped = System.Text.Json.JsonSerializer.Serialize(token);
                await Response.WriteAsync($"data: {{\"token\":{escaped}}}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming error for conversation {ConversationId}", request.ConversationId);
            await Response.WriteAsync($"data: {{\"error\":\"{ex.Message}\"}}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
