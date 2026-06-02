using AssistantApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AssistantApi.Application.Configuration;

namespace AssistantApi.Application.Agents;

public class CodingAgent : IAgent
{
    private readonly IOllamaClient _ollama;
    private readonly AssistantOptions _options;
    private readonly ILogger<CodingAgent> _logger;

    public string Name => "CodingAgent";

    public CodingAgent(IOllamaClient ollama, IOptions<AssistantOptions> options, ILogger<CodingAgent> logger)
    {
        _ollama = ollama;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            var messages = BuildMessages(context);
            _logger.LogInformation("CodingAgent calling Ollama model {Model}", _options.ChatModel);

            var response = await _ollama.ChatAsync(_options.ChatModel, messages, context.CancellationToken);
            return new AgentResult { Success = true, Response = response };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CodingAgent failed for conversation {ConversationId}", context.ConversationId);
            return new AgentResult { Success = false, ErrorMessage = ex.Message, Response = "I encountered an error generating a response. Please try again." };
        }
    }

    private List<ChatMessage> BuildMessages(AgentContext context)
    {
        var messages = new List<ChatMessage>();

        var systemParts = new List<string>
        {
            "You are an expert software engineering assistant. Provide accurate, concise, and idiomatic code."
        };

        if (context.InstructionRules.Count > 0)
        {
            systemParts.Add("## Coding Standards\n" + string.Join("\n", context.InstructionRules));
        }

        messages.Add(new ChatMessage("system", string.Join("\n\n", systemParts)));

        if (context.RetrievedChunks.Count > 0)
        {
            var contextBlock = string.Join("\n\n", context.RetrievedChunks.Select(c =>
                $"// {c.FilePath} ({c.Repository})\n{c.Content}"));
            messages.Add(new ChatMessage("user", $"## Relevant Code Context\n```\n{contextBlock}\n```"));
            messages.Add(new ChatMessage("assistant", "I have reviewed the relevant code context. How can I help?"));
        }

        messages.Add(new ChatMessage("user", context.UserMessage));
        return messages;
    }
}
