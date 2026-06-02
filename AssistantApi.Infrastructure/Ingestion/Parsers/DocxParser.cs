using AssistantApi.Core.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssistantApi.Infrastructure.Ingestion.Parsers;

public class DocxParser : IDocumentParser
{
    private readonly IChunkingService _chunker;

    public DocxParser(IChunkingService chunker) => _chunker = chunker;

    public bool CanParse(string extension) =>
        extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var doc = WordprocessingDocument.Open(filePath, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return Task.FromResult<IReadOnlyList<ParsedChunk>>([]);

        var paragraphs = body.Descendants<Paragraph>()
            .Select(p => p.InnerText.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var fullText = string.Join("\n\n", paragraphs);
        var rawChunks = _chunker.Chunk(fullText, chunkSize: 512, overlap: 64);

        var chunks = rawChunks.Select((c, i) => new ParsedChunk
        {
            Content = c,
            ChunkIndex = i,
            Metadata = new Dictionary<string, string> { ["doc_type"] = "docx" }
        }).ToList();

        return Task.FromResult<IReadOnlyList<ParsedChunk>>(chunks);
    }
}
