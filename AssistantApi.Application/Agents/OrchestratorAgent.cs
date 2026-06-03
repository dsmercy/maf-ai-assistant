using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Agents;

public class OrchestratorAgent : IAgent
{
    private readonly InstructionAgent _instructionAgent;
    private readonly RepositoryAgent _repositoryAgent;
    private readonly CodingAgent _codingAgent;
    private readonly ILogger<OrchestratorAgent> _logger;

    public string Name => "OrchestratorAgent";

    public OrchestratorAgent(
        InstructionAgent instructionAgent,
        RepositoryAgent repositoryAgent,
        CodingAgent codingAgent,
        ILogger<OrchestratorAgent> logger)
    {
        _instructionAgent = instructionAgent;
        _repositoryAgent = repositoryAgent;
        _codingAgent = codingAgent;
        _logger = logger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        context.Intent = ClassifyIntent(context.UserMessage);
        _logger.LogInformation("Intent={Intent} Conversation={ConversationId}", context.Intent, context.ConversationId);

        // Step 1 — Always retrieve coding standards first
        await _instructionAgent.ExecuteAsync(context);

        // Step 2 — Retrieve repository context for code-aware intents
        if (RequiresRepositoryContext(context.Intent))
            await _repositoryAgent.ExecuteAsync(context);

        // Step 3 — Generate response
        var result = await _codingAgent.ExecuteAsync(context);

        sw.Stop();
        result.LatencyMs = sw.ElapsedMilliseconds;
        result.Intent = context.Intent;
        return result;
    }

    public async IAsyncEnumerable<string> StreamAsync(AgentContext context)
    {
        context.Intent = ClassifyIntent(context.UserMessage);
        _logger.LogInformation("Stream Intent={Intent} Conversation={ConversationId}", context.Intent, context.ConversationId);

        await _instructionAgent.ExecuteAsync(context);

        if (RequiresRepositoryContext(context.Intent))
            await _repositoryAgent.ExecuteAsync(context);

        await foreach (var token in _codingAgent.StreamAsync(context))
            yield return token;
    }

    private static AgentIntent ClassifyIntent(string message)
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

    private static bool RequiresRepositoryContext(AgentIntent intent) => intent is
        AgentIntent.CodeReview or
        AgentIntent.RepositoryQuestion or
        AgentIntent.CodeExplanation or
        AgentIntent.CodeGeneration;
}
