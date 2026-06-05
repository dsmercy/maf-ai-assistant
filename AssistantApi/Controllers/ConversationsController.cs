using AssistantApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationRepository _conversations;

    public ConversationsController(IConversationRepository conversations)
        => _conversations = conversations;

    [HttpGet]
    public async Task<IActionResult> GetMyConversations([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var userId = HttpContext.Items["UserId"] as string
                     ?? User.Identity?.Name
                     ?? "anonymous";

        var conversations = await _conversations.GetByUserAsync(userId, limit, ct);

        return Ok(conversations.Select(c => new
        {
            c.Id,
            c.UserId,
            c.CreatedAt,
            c.UpdatedAt,
            MessageCount = c.Messages.Count,
            LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.Content?[..Math.Min(100, c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.Content?.Length ?? 0)]
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var conversation = await _conversations.GetAsync(id, ct);
        if (conversation is null) return NotFound();
        return Ok(conversation);
    }
}
