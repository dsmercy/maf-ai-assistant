namespace AssistantApi.Validators;

/// <summary>Validates that a file upload has an allowed extension, non-empty content, and is within size limits.</summary>
public static class FileUploadValidator
{
    private const long MaxFileSizeBytes = 500 * 1024 * 1024; // 500 MB

    private static readonly Dictionary<string, string[]> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = ["application/pdf"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"],
        [".md"]   = ["text/markdown", "text/plain", "application/octet-stream"],
        [".txt"]  = ["text/plain", "application/octet-stream"],
        [".zip"]  = ["application/zip", "application/x-zip-compressed", "application/octet-stream"]
    };

    /// <summary>
    /// Validates an uploaded file. Returns a list of validation error messages (empty if valid).
    /// </summary>
    public static List<string> Validate(IFormFile file, IEnumerable<string> allowedExtensions)
    {
        var errors = new List<string>();

        if (file is null || file.Length == 0)
        {
            errors.Add("No file provided or file is empty.");
            return errors;
        }

        if (file.Length > MaxFileSizeBytes)
            errors.Add($"File size {file.Length / (1024 * 1024)} MB exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = allowedExtensions.Select(e => e.ToLowerInvariant()).ToList();

        if (!allowed.Contains(ext))
            errors.Add($"File type '{ext}' is not allowed. Allowed types: {string.Join(", ", allowed)}");
        else if (AllowedMimeTypes.TryGetValue(ext, out var validMimes))
        {
            if (!validMimes.Any(m => file.ContentType.StartsWith(m, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Content type '{file.ContentType}' does not match expected type for '{ext}'.");
        }

        return errors;
    }
}
