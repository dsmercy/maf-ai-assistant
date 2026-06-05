using AssistantApi.Core.Interfaces;
using Markdig;

namespace AssistantApi.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Parses Markdown files into chunks for embedding.
/// Supports YAML front matter for tagging instruction files with language and category metadata.
///
/// Front matter format (optional, at top of file):
/// ---
/// language: csharp          (csharp, typescript, javascript, python, go, etc.)
/// category: ef-core          (any descriptive tag)
/// ---
///
/// These tags are stored as Qdrant metadata so InstructionAgent can filter
/// instructions by language, preventing Python rules from being injected
/// into a C# response and vice versa.
/// </summary>
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

        // Extract YAML front matter if present
        var (frontMatter, body) = ExtractFrontMatter(raw);

        // Strip markdown syntax for cleaner embedding
        var plainText = Markdown.ToPlainText(body, Pipeline);

        var rawChunks = _chunker.Chunk(plainText, chunkSize: 512, overlap: 64);

        return rawChunks.Select((c, i) =>
        {
            var meta = new Dictionary<string, string>(frontMatter)
            {
                ["doc_type"] = "markdown"
            };
            return new ParsedChunk { Content = c, ChunkIndex = i, Metadata = meta };
        }).ToList();
    }

    /// <summary>
    /// Parses YAML front matter delimited by --- at the top of the file.
    /// Extracts key: value pairs into a metadata dictionary.
    /// Returns the metadata and the body with front matter stripped.
    /// </summary>
    private static (Dictionary<string, string> FrontMatter, string Body) ExtractFrontMatter(string content)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!content.TrimStart().StartsWith("---"))
            return (meta, content);

        var lines = content.Split('\n');
        var inFrontMatter = false;
        var endLine = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (i == 0 && line == "---") { inFrontMatter = true; continue; }
            if (inFrontMatter && line == "---") { endLine = i; break; }

            if (inFrontMatter)
            {
                var colon = line.IndexOf(':');
                if (colon > 0)
                {
                    var key = line[..colon].Trim().ToLowerInvariant();
                    var value = line[(colon + 1)..].Trim();
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        meta[key] = value;
                }
            }
        }

        var body = endLine >= 0
            ? string.Join('\n', lines[(endLine + 1)..])
            : content;

        return (meta, body);
    }
}
