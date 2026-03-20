namespace AdrPortal.Web.Services;

/// <summary>
/// Represents extracted structured ADR sections used for richer detail rendering.
/// </summary>
public sealed record AdrStructuredSectionsViewModel
{
    /// <summary>
    /// Gets the selected option statement from decision outcome.
    /// </summary>
    public string? SelectedOption { get; init; }

    /// <summary>
    /// Gets the rationale text from decision outcome.
    /// </summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// Gets considered options parsed from ADR markdown.
    /// </summary>
    public required IReadOnlyList<AdrDecisionOptionViewModel> ConsideredOptions { get; init; }

    /// <summary>
    /// Gets consequence bullets tagged as positive.
    /// </summary>
    public required IReadOnlyList<string> PositiveConsequences { get; init; }

    /// <summary>
    /// Gets consequence bullets tagged as negative.
    /// </summary>
    public required IReadOnlyList<string> NegativeConsequences { get; init; }
}
