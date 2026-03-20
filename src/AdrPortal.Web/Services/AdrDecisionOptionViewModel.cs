namespace AdrPortal.Web.Services;

/// <summary>
/// Represents a single ADR option with extracted pros and cons for detail rendering.
/// </summary>
public sealed record AdrDecisionOptionViewModel
{
    /// <summary>
    /// Gets the option name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the option summary text.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the extracted pros for this option.
    /// </summary>
    public required IReadOnlyList<string> Pros { get; init; }

    /// <summary>
    /// Gets the extracted cons for this option.
    /// </summary>
    public required IReadOnlyList<string> Cons { get; init; }
}
