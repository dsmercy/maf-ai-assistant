namespace AssistantApi.Core.Interfaces;

public interface IDocumentParser
{
    bool CanParse(string extension);
    Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, CancellationToken ct = default);
}

public class ParsedChunk
{
    public string Content { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}
