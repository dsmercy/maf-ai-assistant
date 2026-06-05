namespace AssistantApi.Core.Entities;

/// <summary>
/// A named boolean flag stored in PostgreSQL for toggling application features
/// at runtime without redeployment.
/// </summary>
public class FeatureFlag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique name used to look up the flag in code (e.g. "streaming", "rag", "auth").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the feature is currently enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Human-readable description of what this flag controls.</summary>
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
