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

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request, CancellationToken ct)
    {
        // Streaming will be fully wired in Phase 4; return same non-streaming response for now
        var userId = User.Identity?.Name ?? "anonymous";
        var response = await _chatService.HandleAsync(request, userId, ct);

        Response.ContentType = "text/event-stream";
        await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(response)}\n\n", ct);
    }
}
