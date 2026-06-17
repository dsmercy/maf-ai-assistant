namespace AssistantApi.Application.Configuration;

public class AssistantOptions
{
    public const string SectionName = "Assistant";

    public string ChatModel { get; set; } = "qwen3-coder:30b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 64;
    /// <summary>Max code chunks retrieved from code-embeddings per request.</summary>
    public int TopK { get; set; } = 5;

    /// <summary>Max instruction rules retrieved from instruction-embeddings per request. Keep low to limit prompt size.</summary>
    public int InstructionTopK { get; set; } = 5;
    public double Temperature { get; set; } = 0.2;

    /// <summary>Embedding vector dimension. Must match the model: nomic-embed-text=768, mxbai-embed-large=1024.</summary>
    public ulong VectorSize { get; set; } = 768;

    /// <summary>When true, uses LlmAgentRouter to pick agents dynamically. Falls back to RulesAgentRouter on error.</summary>
    public bool UseAiRouter { get; set; } = false;

    /// <summary>Ollama model for the LLM router. Defaults to ChatModel when empty. Use a smaller/faster model here.</summary>
    public string RouterModel { get; set; } = string.Empty;
}
