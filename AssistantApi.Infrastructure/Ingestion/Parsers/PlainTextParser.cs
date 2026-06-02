using AssistantApi.Core.Interfaces;

namespace AssistantApi.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Handles .txt, .sql, .html, .css, .scss and all source code extensions.
/// </summary>
public class PlainTextParser : IDocumentParser
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".go", ".java", ".cpp", ".c", ".h",
        ".rs", ".rb", ".php", ".swift", ".kt", ".json", ".xml", ".yml", ".yaml",
        ".sql", ".html", ".css", ".scss", ".sass", ".txt", ".sh", ".ps1", ".env",
        ".toml", ".ini", ".cfg", ".config"
    };

    private readonly IChunkingService _chunker;

    public PlainTextParser(IChunkingService chunker) => _chunker = chunker;

    public bool CanParse(string extension) => SupportedExtensions.Contains(extension);

    public async Task<IReadOnlyList<ParsedChunk>> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(filePath, ct);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var docType = IsSourceCode(ext) ? "source_code" : "text";

        var rawChunks = _chunker.Chunk(text, chunkSize: 512, overlap: 64);
        return rawChunks.Select((c, i) => new ParsedChunk
        {
            Content = c,
            ChunkIndex = i,
            Metadata = new Dictionary<string, string>
            {
                ["doc_type"] = docType,
                ["language"] = ExtensionToLanguage(ext)
            }
        }).ToList();
    }

    private static bool IsSourceCode(string ext) => ext is
        ".cs" or ".js" or ".ts" or ".tsx" or ".jsx" or ".py" or ".go" or ".java" or
        ".cpp" or ".c" or ".h" or ".rs" or ".rb" or ".php" or ".swift" or ".kt";

    private static string ExtensionToLanguage(string ext) => ext switch
    {
        ".cs" => "csharp",
        ".js" => "javascript",
        ".ts" or ".tsx" => "typescript",
        ".py" => "python",
        ".go" => "go",
        ".java" => "java",
        ".rs" => "rust",
        ".rb" => "ruby",
        ".php" => "php",
        ".cpp" or ".c" or ".h" => "c_cpp",
        ".sql" => "sql",
        ".json" => "json",
        ".xml" => "xml",
        ".yml" or ".yaml" => "yaml",
        ".html" => "html",
        ".css" or ".scss" or ".sass" => "css",
        _ => "text"
    };
}
