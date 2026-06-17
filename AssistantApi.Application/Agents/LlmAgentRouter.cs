using System.Text.Json;
using AssistantApi.Application.Configuration;
using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantApi.Application.Agents;

/// <summary>
/// Asks the LLM which agents to run for a given request.
/// Falls back to RulesAgentRouter on any error or if the model returns invalid agent names.
/// The router model is typically a smaller/faster model than the chat model.
/// </summary>
public sealed class LlmAgentRouter : IAgentRouter
{
    private readonly IOllamaClient _ollama;
    private readonly RulesAgentRouter _fallback;
    private readonly AssistantOptions _options;
    private readonly ILogger<LlmAgentRouter> _logger;

    public LlmAgentRouter(
        IOllamaClient ollama,
        RulesAgentRouter fallback,
        IOptions<AssistantOptions> options,
        ILogger<LlmAgentRouter> logger)
    {
        _ollama   = ollama;
        _fallback = fallback;
        _options  = options.Value;
        _logger   = logger;
    }

    public async Task<IReadOnlyList<string>> RouteAsync(
        AgentContext context, IReadOnlyList<string> availableAgents, CancellationToken ct = default)
    {
        var routerModel = string.IsNullOrWhiteSpace(_options.RouterModel)
            ? _options.ChatModel
            : _options.RouterModel;

        var agentList = string.Join(", ", availableAgents);
        var systemPrompt =
            "You are a routing assistant. Given a user message, decide which agents to run and in what order. " +
            $"Available agents: [{agentList}]. " +
            "Reply with ONLY a JSON array of agent names, e.g. [\"InstructionAgent\",\"CodingAgent\"]. " +
            "No explanation, no markdown.";

        var userPrompt = $"User message: {context.UserMessage[..Math.Min(context.UserMessage.Length, 500)]}";

        try
        {
            var messages = new List<ChatMessage>
            {
                new("system", systemPrompt),
                new("user",   userPrompt)
            };

            var response = await _ollama.ChatAsync(routerModel, messages, ct);
            var trimmed  = response.Trim().TrimStart('`').TrimEnd('`');
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[4..].Trim();

            var names = JsonSerializer.Deserialize<List<string>>(trimmed) ?? [];

            // Validate — only accept names the registry actually contains
            var valid = names.Where(availableAgents.Contains).ToList();
            if (valid.Count == 0)
            {
                _logger.LogWarning("LlmAgentRouter returned no valid agent names, falling back to rules router");
                return await _fallback.RouteAsync(context, availableAgents, ct);
            }

            _logger.LogInformation("LlmAgentRouter selected agents: [{Agents}]", string.Join(", ", valid));
            return valid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LlmAgentRouter failed, falling back to rules router");
            return await _fallback.RouteAsync(context, availableAgents, ct);
        }
    }
}
