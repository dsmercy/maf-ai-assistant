using AssistantApi.Core.Interfaces;
using UglyToad.PdfPig;

namespace AssistantApi.Infrastructure.Ingestion.Parsers;

public class PdfParser : IDocumentParser
{
    private readonly IChunkingService _chunker;

    public PdfParser(IChunkingService chunker) => _chunker = chunker;

    public bool CanParse(string extension) =>
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, CancellationToken ct = default)
    {
        using var doc = PdfDocument.Open(filePath);
        var pageTexts = doc.GetPages()
            .Select((page, i) => (text: page.Text, pageNum: i + 1))
            .Where(p => !string.IsNullOrWhiteSpace(p.text))
            .ToList();

        var chunks = new List<ParsedChunk>();
        var chunkIndex = 0;

        foreach (var (text, pageNum) in pageTexts)
        {
            // Each page is already a natural chunk boundary; split further if large
            var pageChunks = _chunker.Chunk(text, chunkSize: 512, overlap: 64);
            foreach (var chunk in pageChunks)
            {
                chunks.Add(new ParsedChunk
                {
                    Content = chunk,
                    ChunkIndex = chunkIndex++,
                    Metadata = new Dictionary<string, string>
                    {
                        ["doc_type"] = "pdf",
                        ["page"] = pageNum.ToString()
                    }
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ParsedChunk>>(chunks);
    }
}
