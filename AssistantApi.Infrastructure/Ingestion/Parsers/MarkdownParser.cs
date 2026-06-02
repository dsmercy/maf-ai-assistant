using AssistantApi.Core.Interfaces;
using Markdig;

namespace AssistantApi.Infrastructure.Ingestion.Parsers;

public class MarkdownParser : IDocumentParser
{
    private readonly IChunkingService _chunker;
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public MarkdownParser(IChunkingService chunker) => _chunker = chunker;

    public bool CanParse(string extension) =>
        extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var raw = await File.ReadAllTextAsync(filePath, ct);
        // Strip markdown syntax to get plain text for embedding
        var plainText = Markdown.ToPlainText(raw, Pipeline);

        var rawChunks = _chunker.Chunk(plainText, chunkSize: 512, overlap: 64);
        return rawChunks.Select((c, i) => new ParsedChunk
        {
            Content = c,
            ChunkIndex = i,
            Metadata = new Dictionary<string, string> { ["doc_type"] = "markdown" }
        }).ToList();
    }
}
