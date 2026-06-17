using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Entry point for the agent pipeline. Resolves agents from IAgentRegistry, routes the request
/// via IAgentRouter, and invokes each agent in order.
///
/// Adding a new agent no longer requires touching this file — register it in Program.cs.
/// </summary>
public class OrchestratorAgent : IAgent
{
    private readonly IAgentRegistry _registry;
    private readonly IAgentRouter _router;
    private readonly IServiceProvider _sp;
    private readonly ILogger<OrchestratorAgent> _logger;

    public string Name => "OrchestratorAgent";

    public OrchestratorAgent(
        IAgentRegistry registry,
        IAgentRouter router,
        IServiceProvider sp,
        ILogger<OrchestratorAgent> logger)
    {
        _registry = registry;
        _router   = router;
        _sp       = sp;
        _logger   = logger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        context.Intent = ClassifyIntent(context.UserMessage);
        context.PublishEvent(new IntentClassifiedEvent(
            Name, DateTimeOffset.UtcNow, context.Intent, _router.GetType().Name));

        _logger.LogInformation("Intent={Intent} Router={Router} Conversation={ConversationId}",
            context.Intent, _router.GetType().Name, context.ConversationId);

        var agentOrder = await _router.RouteAsync(
            context,
            _registry.Registrations.Select(r => r.Name).ToList(),
            context.CancellationToken);

        AgentResult? lastResult = null;

        foreach (var name in agentOrder)
        {
            var reg = _registry.Registrations.FirstOrDefault(r => r.Name == name);
            if (reg is null)
            {
                _logger.LogWarning("Router returned unknown agent name '{Name}', skipping", name);
                continue;
            }

            if (!reg.Condition(context))
            {
                _logger.LogDebug("Agent {Name} condition false — skipped", name);
                continue;
            }

            var agent = reg.Factory(_sp);
            lastResult = await agent.ExecuteAsync(context);

            if (!lastResult.Success)
                _logger.LogWarning("Agent {Name} reported failure: {Error}", name, lastResult.ErrorMessage);
        }

        sw.Stop();
        var result = lastResult ?? new AgentResult { Success = true };
        result.LatencyMs = sw.ElapsedMilliseconds;
        result.Intent    = context.Intent;
        return result;
    }

    /// <summary>
    /// Streams tokens from the first registered IStreamingAgent in the routed order.
    /// Non-streaming agents still run (retrieval, instructions) before the streaming agent fires.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(AgentContext context)
    {
        context.Intent = ClassifyIntent(context.UserMessage);
        context.PublishEvent(new IntentClassifiedEvent(
            Name, DateTimeOffset.UtcNow, context.Intent, _router.GetType().Name));

        _logger.LogInformation("Stream Intent={Intent} Conversation={ConversationId}", context.Intent, context.ConversationId);

        var agentOrder = await _router.RouteAsync(
            context,
            _registry.Registrations.Select(r => r.Name).ToList(),
            context.CancellationToken);

        IStreamingAgent? streamingAgent = null;

        foreach (var name in agentOrder)
        {
            var reg = _registry.Registrations.FirstOrDefault(r => r.Name == name);
            if (reg is null || !reg.Condition(context)) continue;

            var agent = reg.Factory(_sp);

            if (agent is IStreamingAgent sa)
            {
                streamingAgent = sa;
                continue; // run at the end after all retrieval agents
            }

            await agent.ExecuteAsync(context);
        }

        if (streamingAgent is not null)
        {
            await foreach (var token in streamingAgent.StreamAsync(context))
                yield return token;
        }
    }

    /// <summary>
    /// Classifies the user's message into one of the AgentIntent categories.
    /// More specific intents are checked before general ones to avoid false matches.
    /// </summary>
    public static AgentIntent ClassifyIntent(string message)
    {
        var lower = message.ToLowerInvariant();

        if (ContainsAny(lower, "unit test", "write test", "test for", "xunit", "nunit", "moq"))
            return AgentIntent.UnitTest;
        if (ContainsAny(lower, "review", "improve", "refactor", "clean up", "optimise", "optimize", "fix"))
            return AgentIntent.CodeReview;
        if (ContainsAny(lower, "generate", "create", "write", "implement", "build", "scaffold", "add"))
            return AgentIntent.CodeGeneration;
        if (ContainsAny(lower, "explain", "what does", "how does", "describe", "what is", "how is"))
            return AgentIntent.CodeExplanation;
        if (ContainsAny(lower, "document", "xml doc", "summary comment", "readme", "docs for"))
            return AgentIntent.Documentation;
        if (ContainsAny(lower, "repository", "repo", "codebase", "file", "class", "method", "namespace", "project"))
            return AgentIntent.RepositoryQuestion;

        return AgentIntent.GeneralQuestion;
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(text.Contains);
}
