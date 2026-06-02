namespace AssistantApi.Application.DTOs;

public class ConfigResponse
{
    public string ChatModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public int TopK { get; set; }
    public double Temperature { get; set; }
    public Dictionary<string, bool> FeatureFlags { get; set; } = [];
}
