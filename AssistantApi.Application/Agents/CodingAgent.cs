using AssistantApi.Application.Configuration;
using AssistantApi.Core.Entities;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

public class CodingAgent : IAgent
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

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            var messages = await BuildMessagesAsync(context);
            _logger.LogInformation("CodingAgent calling {Model} for intent {Intent} conversation {ConversationId}",
                _options.ChatModel, context.Intent, context.ConversationId);

            var response = await _ollama.ChatAsync(_options.ChatModel, messages, context.CancellationToken);
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

    public async IAsyncEnumerable<string> StreamAsync(AgentContext context)
    {
        var messages = await BuildMessagesAsync(context);
        _logger.LogInformation("CodingAgent streaming {Model} for intent {Intent}", _options.ChatModel, context.Intent);

        await foreach (var token in _ollama.ChatStreamAsync(_options.ChatModel, messages, context.CancellationToken))
            yield return token;
    }

    private async Task<List<ChatMessage>> BuildMessagesAsync(AgentContext context)
    {
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
            // Fallback when no template exists in DB yet
            systemPrompt = BuildFallbackSystemPrompt(instructionsText, contextChunksText);
            userMessage = context.UserMessage;
        }

        return
        [
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userMessage)
        ];
    }

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

    private static string DetectLanguage(AgentContext context)
    {
        if (context.RetrievedChunks.Count == 0) return "C#";
        var langs = context.RetrievedChunks
            .Select(c => c.Language)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .GroupBy(l => l)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        return langs ?? "C#";
    }
}
