using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Agents;

public class OrchestratorAgent : IAgent
{
    private readonly IAgent _repositoryAgent;
    private readonly IAgent _instructionAgent;
    private readonly IAgent _codingAgent;
    private readonly ILogger<OrchestratorAgent> _logger;

    public string Name => "OrchestratorAgent";

    public OrchestratorAgent(
        RepositoryAgent repositoryAgent,
        InstructionAgent instructionAgent,
        CodingAgent codingAgent,
        ILogger<OrchestratorAgent> logger)
    {
        _repositoryAgent = repositoryAgent;
        _instructionAgent = instructionAgent;
        _codingAgent = codingAgent;
        _logger = logger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        context.Intent = ClassifyIntent(context.UserMessage);
        _logger.LogInformation("Classified intent as {Intent} for conversation {ConversationId}",
            context.Intent, context.ConversationId);

        // Always retrieve coding standards first
        await _instructionAgent.ExecuteAsync(context);

        // Retrieve repository context for code-related intents
        if (RequiresRepositoryContext(context.Intent))
            await _repositoryAgent.ExecuteAsync(context);

        var result = await _codingAgent.ExecuteAsync(context);

        sw.Stop();
        result.LatencyMs = sw.ElapsedMilliseconds;
        result.Intent = context.Intent;
        return result;
    }

    private static AgentIntent ClassifyIntent(string message)
    {
        var lower = message.ToLowerInvariant();

        if (ContainsAny(lower, "generate", "create", "write", "implement", "build"))
            return AgentIntent.CodeGeneration;
        if (ContainsAny(lower, "explain", "what does", "how does", "describe"))
            return AgentIntent.CodeExplanation;
        if (ContainsAny(lower, "review", "improve", "refactor", "clean up", "optimise", "optimize"))
            return AgentIntent.CodeReview;
        if (ContainsAny(lower, "unit test", "test for", "write test", "xunit", "nunit"))
            return AgentIntent.UnitTest;
        if (ContainsAny(lower, "document", "xml doc", "summary", "readme"))
            return AgentIntent.Documentation;
        if (ContainsAny(lower, "repository", "repo", "codebase", "file", "class", "method"))
            return AgentIntent.RepositoryQuestion;

        return AgentIntent.GeneralQuestion;
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(text.Contains);

    private static bool RequiresRepositoryContext(AgentIntent intent) => intent is
        AgentIntent.CodeReview or
        AgentIntent.RepositoryQuestion or
        AgentIntent.CodeExplanation;
}
