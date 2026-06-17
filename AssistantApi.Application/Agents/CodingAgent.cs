using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// The final agent in the pipeline. Assembles the full LLM prompt from the context
/// populated by InstructionAgent and RepositoryAgent, then calls the Ollama model.
///
/// Prompt assembly:
///   - Loads a PromptTemplate from PostgreSQL matching the detected intent
///   - Fills placeholders: {instructions}, {context_chunks}, {user_message}, {language}
///   - Falls back to a hardcoded default if no template exists in the database
///
/// Provides both blocking (ExecuteAsync) and streaming (StreamAsync) variants.
/// Implements IStreamingAgent so OrchestratorAgent can discover it via the registry.
/// </summary>
public class CodingAgent : IStreamingAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IPromptTemplateRepository _templates;
    private readonly AssistantOptions _options;
    private readonly ILogger<CodingAgent> _logger;

    public string Name => "CodingAgent";

    public CodingAgent(
        IOllamaClient ollama,
        IPromptTemplateRepository templates,
        IOptions<AssistantOptions> options,
        ILogger<CodingAgent> logger)
    {
        _ollama = ollama;
        _templates = templates;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Builds the prompt and calls the LLM synchronously, waiting for the full response.
    /// Used by POST /api/chat.
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var messages = await BuildMessagesAsync(context);
            _logger.LogInformation("CodingAgent calling {Model} for intent {Intent} conversation {ConversationId}",
                _options.ChatModel, context.Intent, context.ConversationId);

            var response = await _ollama.ChatAsync(_options.ChatModel, messages, context.CancellationToken);
            sw.Stop();
            context.PublishEvent(new ResponseGeneratedEvent(Name, DateTimeOffset.UtcNow, sw.ElapsedMilliseconds, Streamed: false));
            return new AgentResult { Success = true, Response = response };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CodingAgent failed for conversation {ConversationId}", context.ConversationId);
            return new AgentResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Response = "I encountered an error generating a response. Please try again."
            };
        }
    }

    /// <summary>
    /// Builds the prompt and streams LLM response tokens as they are generated.
    /// Used by POST /api/chat/stream and POST /v1/chat/completions (stream:true).
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(AgentContext context)
    {
        var messages = await BuildMessagesAsync(context);
        _logger.LogInformation("CodingAgent streaming {Model} for intent {Intent}", _options.ChatModel, context.Intent);

        await foreach (var token in _ollama.ChatStreamAsync(_options.ChatModel, messages, context.CancellationToken))
            yield return token;
    }

    /// <summary>
    /// Loads the appropriate prompt template from PostgreSQL and fills all placeholders
    /// with the instruction rules, retrieved code chunks, user message, and detected language.
    /// Falls back to a hardcoded default if no template is found.
    /// </summary>
    private async Task<List<ChatMessage>> BuildMessagesAsync(AgentContext context)
    {
        if (context.MessagesOverride is { Count: > 0 })
            return context.MessagesOverride;

        var template = await _templates.GetByTaskTypeAsync(context.Intent.ToString(), context.CancellationToken);

        var language = DetectLanguage(context);
        var instructionsText = context.InstructionRules.Count > 0
            ? string.Join("\n", context.InstructionRules.Select((r, i) => $"{i + 1}. {r}"))
            : string.Empty;

        var contextChunksText = context.RetrievedChunks.Count > 0
            ? string.Join("\n\n", context.RetrievedChunks.Select(c =>
                $"// File: {c.FilePath} | Repo: {c.Repository} | Score: {c.Score:F2}\n{c.Content}"))
            : string.Empty;

        string systemPrompt;
        string userMessage;

        if (template is not null)
        {
            systemPrompt = template.SystemPrompt
                .Replace("{instructions}", instructionsText)
                .Replace("{context_chunks}", contextChunksText)
                .Replace("{language}", language);

            userMessage = template.UserPromptTemplate
                .Replace("{user_message}", context.UserMessage)
                .Replace("{language}", language)
                .Replace("{instructions}", instructionsText)
                .Replace("{context_chunks}", contextChunksText);
        }
        else
        {
            systemPrompt = BuildFallbackSystemPrompt(instructionsText, contextChunksText);
            userMessage = context.UserMessage;
        }

        return
        [
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userMessage)
        ];
    }

    /// <summary>
    /// Builds a basic system prompt when no database template is available.
    /// Includes coding standards and code context if they were retrieved.
    /// </summary>
    private static string BuildFallbackSystemPrompt(string instructions, string contextChunks)
    {
        var parts = new List<string>
        {
            "You are an expert software engineering assistant. Provide accurate, concise, and idiomatic code."
        };
        if (!string.IsNullOrWhiteSpace(instructions))
            parts.Add($"## Coding Standards\n{instructions}");
        if (!string.IsNullOrWhiteSpace(contextChunks))
            parts.Add($"## Relevant Code Context\n```\n{contextChunks}\n```");
        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Detects the dominant programming language from the retrieved code chunks
    /// by finding the most frequently occurring language in the results.
    /// Defaults to "C#" when no chunks were retrieved.
    /// </summary>
    private static string DetectLanguage(AgentContext context)
    {
        if (context.RetrievedChunks.Count == 0) return "C#";
        return context.RetrievedChunks
            .Select(c => c.Language)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .GroupBy(l => l)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "C#";
    }
}
