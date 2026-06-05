namespace AssistantApi.Infrastructure.Ingestion;

/// <summary>
/// Filters the files in a cloned repository to include only those that are
/// meaningful for code indexing, excluding build artifacts, dependencies,
/// and IDE-specific directories.
///
/// Allowed extensions cover all major source code, config, and documentation
/// file types. Ignored directories prevent embedding generated files like
/// node_modules, bin/obj, and .git history metadata.
/// </summary>
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

    /// <summary>
    /// Enumerates all files under <paramref name="rootPath"/> that have an allowed extension
    /// and are not inside an ignored directory.
    /// </summary>
    /// <param name="rootPath">Root directory of the cloned repository.</param>
    /// <returns>Filtered sequence of absolute file paths.</returns>
    public static IEnumerable<string> GetIndexableFiles(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .Where(f => IsAllowed(f, rootPath));
    }

    /// <summary>
    /// Returns true if the file has an allowed extension and none of its
    /// parent directory segments are in the ignored list.
    /// </summary>
    private static bool IsAllowed(string filePath, string rootPath)
    {
        var ext = Path.GetExtension(filePath);
        if (!AllowedExtensions.Contains(ext)) return false;

        var relative = Path.GetRelativePath(rootPath, filePath);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !parts.Any(p => IgnoredDirectories.Contains(p));
    }
}
