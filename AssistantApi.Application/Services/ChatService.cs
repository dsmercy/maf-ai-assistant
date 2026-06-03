using System.Runtime.CompilerServices;
using AssistantApi.Application.Agents;
using AssistantApi.Application.DTOs;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Services;

public class ChatService
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly ILogger<ChatService> _logger;

    public ChatService(OrchestratorAgent orchestrator, ILogger<ChatService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<ChatResponse> HandleAsync(ChatRequest request, string userId, CancellationToken ct)
    {
        var context = new AgentContext
        {
            UserMessage = request.Message,
            ConversationId = request.ConversationId,
            UserId = userId,
            RepositoryFilter = request.RepositoryFilter,
            CancellationToken = ct
        };

        _logger.LogInformation("Chat request for conversation {ConversationId}", request.ConversationId);

        var result = await _orchestrator.ExecuteAsync(context);

        return new ChatResponse
        {
            ConversationId = request.ConversationId,
            Response = result.Response,
            Intent = result.Intent.ToString(),
            LatencyMs = result.LatencyMs,
            Sources = context.RetrievedChunks.Select(c => new SourceReference
            {
                FilePath = c.FilePath,
                Repository = c.Repository,
                Score = c.Score
            }).ToList()
        };
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        string userId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var context = new AgentContext
        {
            UserMessage = request.Message,
            ConversationId = request.ConversationId,
            UserId = userId,
            RepositoryFilter = request.RepositoryFilter,
            CancellationToken = ct
        };

        await foreach (var token in _orchestrator.StreamAsync(context))
            yield return token;
    }
}
