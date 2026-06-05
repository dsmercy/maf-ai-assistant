using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssistantApi.Infrastructure.Persistence;

public class ConversationRepository : IConversationRepository
{
    private readonly AssistantDbContext _db;

    public ConversationRepository(AssistantDbContext db) => _db = db;

    public Task<Conversation?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Conversation?> GetByStringIdAsync(string conversationId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(conversationId, out var guid))
            return Task.FromResult<Conversation?>(null);
        return GetAsync(guid, ct);
    }

    public async Task<IReadOnlyList<Conversation>> GetByUserAsync(string userId, int limit = 20, CancellationToken ct = default)
        => await _db.Conversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(limit)
            .Include(c => c.Messages)
            .ToListAsync(ct);

    public async Task<Conversation> AddMessageAsync(
        string conversationId, string userId, string role, string content,
        AgentIntent? intent = null, long? latencyMs = null, CancellationToken ct = default)
    {
        Conversation? conversation = null;

        if (Guid.TryParse(conversationId, out var guid))
            conversation = await _db.Conversations.Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == guid, ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.TryParse(conversationId, out var parsedGuid) ? parsedGuid : Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Conversations.Add(conversation);
        }

        conversation.Messages.Add(new ConversationMessage
        {
            ConversationId = conversation.Id,
            Role = role,
            Content = content,
            DetectedIntent = intent,
            LatencyMs = latencyMs,
            CreatedAt = DateTime.UtcNow
        });
        conversation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return conversation;
    }
}
