using System.Runtime.CompilerServices;
using AssistantApi.Application.Agents;
using AssistantApi.Application.DTOs;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Services;

/// <summary>
/// Application-layer service that coordinates a complete chat request.
/// Orchestrates the agent pipeline, persists conversation history, and
/// maps the result to the API response DTO.
///
/// Used by both ChatController (native /api/chat endpoint) and
/// OpenAiController (/v1/chat/completions for Open WebUI compatibility).
/// </summary>
public class ChatService
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly IConversationRepository _conversations;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        OrchestratorAgent orchestrator,
        IConversationRepository conversations,
        ILogger<ChatService> logger)
    {
        _orchestrator = orchestrator;
        _conversations = conversations;
        _logger = logger;
    }

    /// <summary>
    /// Handles a non-streaming chat request end-to-end:
    /// persists the user message, runs the agent pipeline, persists the response,
    /// and returns a ChatResponse with sources and latency.
    /// </summary>
    /// <param name="request">The incoming chat request with message and conversation ID.</param>
    /// <param name="userId">Identity of the requesting user extracted from JWT or "anonymous".</param>
    /// <param name="ct">Cancellation token from the HTTP request.</param>
    public async Task<ChatResponse> HandleAsync(ChatRequest request, string userId, CancellationToken ct)
    {
        var context = new AgentContext
        {
            UserMessage = request.Message,
            ConversationId = request.ConversationId,
            UserId = userId,
            RepositoryFilter = request.RepositoryFilter,
            MessagesOverride = request.MessagesOverride,
            CancellationToken = ct
        };

        _logger.LogInformation("Chat request for conversation {ConversationId}", request.ConversationId);

        await _conversations.AddMessageAsync(request.ConversationId, userId, "user", request.Message, ct: ct);

        var result = await _orchestrator.ExecuteAsync(context);

        await _conversations.AddMessageAsync(request.ConversationId, userId, "assistant",
            result.Response, result.Intent, result.LatencyMs, ct);

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

    /// <summary>
    /// Handles a streaming chat request by running the agent pipeline and
    /// yielding response tokens as they are produced by the LLM.
    /// Used by /api/chat/stream and /v1/chat/completions with stream:true.
    /// </summary>
    /// <param name="request">The incoming chat request.</param>
    /// <param name="userId">Identity of the requesting user.</param>
    /// <param name="ct">Cancellation token — cancel to stop the stream.</param>
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
            MessagesOverride = request.MessagesOverride,
            CancellationToken = ct
        };

        await foreach (var token in _orchestrator.StreamAsync(context))
            yield return token;
    }
}
