using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Entry point for the agent pipeline. Coordinates all other agents in the correct order.
///
/// Execution order for every request:
///   1. ClassifyIntent — determine what the user wants
///   2. InstructionAgent — always fetch coding standards from Qdrant
///   3. RepositoryAgent — conditionally fetch relevant code from Qdrant
///   4. CodingAgent — build the prompt and call the LLM
///
/// Also exposes StreamAsync for token-by-token streaming responses.
/// </summary>
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

    /// <summary>
    /// Runs the full agent pipeline and returns the complete response as a single string.
    /// Used by POST /api/chat (non-streaming).
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        context.Intent = ClassifyIntent(context.UserMessage);
        _logger.LogInformation("Intent={Intent} Conversation={ConversationId}", context.Intent, context.ConversationId);

        await _instructionAgent.ExecuteAsync(context);

        if (RequiresRepositoryContext(context.Intent))
            await _repositoryAgent.ExecuteAsync(context);

        var result = await _codingAgent.ExecuteAsync(context);

        sw.Stop();
        result.LatencyMs = sw.ElapsedMilliseconds;
        result.Intent = context.Intent;
        return result;
    }

    /// <summary>
    /// Runs the agent pipeline and yields response tokens as they are produced by the LLM.
    /// Used by POST /api/chat/stream and POST /v1/chat/completions (with stream:true).
    /// </summary>
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

    /// <summary>
    /// Classifies the user's message into one of the AgentIntent categories
    /// by searching for characteristic keywords. More specific intents are
    /// checked before more general ones to avoid false matches.
    /// </summary>
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

    /// <summary>
    /// Returns true if the classified intent should trigger a Qdrant search for repository context.
    /// Intents that generate or analyze code benefit from real codebase context.
    /// Intents like UnitTest or Documentation for new content do not need existing code.
    /// </summary>
    private static bool RequiresRepositoryContext(AgentIntent intent) => intent is
        AgentIntent.CodeReview or
        AgentIntent.RepositoryQuestion or
        AgentIntent.CodeExplanation or
        AgentIntent.CodeGeneration;
}
