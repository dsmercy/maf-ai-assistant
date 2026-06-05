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
}
