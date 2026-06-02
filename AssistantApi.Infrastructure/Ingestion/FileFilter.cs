namespace AssistantApi.Infrastructure.Ingestion;

public static class FileFilter
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".go", ".java", ".cpp", ".c", ".h",
        ".rs", ".rb", ".php", ".swift", ".kt", ".json", ".xml", ".yml", ".yaml",
        ".md", ".markdown", ".sql", ".html", ".css", ".scss", ".sass", ".txt",
        ".sh", ".ps1", ".toml", ".ini", ".cfg", ".config"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".idea", ".vscode",
        "dist", "build", "out", "target", "packages", ".nuget", "__pycache__",
        ".mypy_cache", "coverage", ".terraform"
    };

    public static IEnumerable<string> GetIndexableFiles(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .Where(f => IsAllowed(f, rootPath));
    }

    private static bool IsAllowed(string filePath, string rootPath)
    {
        var ext = Path.GetExtension(filePath);
        if (!AllowedExtensions.Contains(ext)) return false;

        // Reject paths that contain any ignored directory segment
        var relative = Path.GetRelativePath(rootPath, filePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !parts.Any(p => IgnoredDirectories.Contains(p));
    }
}
