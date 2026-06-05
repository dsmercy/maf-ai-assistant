using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Conversation?> GetByStringIdAsync(string conversationId, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetByUserAsync(string userId, int limit = 20, CancellationToken ct = default);
    Task<Conversation> AddMessageAsync(string conversationId, string userId, string role, string content, AgentIntent? intent = null, long? latencyMs = null, CancellationToken ct = default);
}
