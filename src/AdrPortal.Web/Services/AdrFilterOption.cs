using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents one ADR status filter option for list views.
/// </summary>
public sealed record AdrFilterOption
{
    /// <summary>
    /// Gets the status represented by this filter option.
    /// </summary>
    public required AdrStatus Status { get; init; }

    /// <summary>
    /// Gets the user-facing status label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the CSS modifier class.
    /// </summary>
    public required string CssModifier { get; init; }

    /// <summary>
    /// Gets the number of ADRs for this status.
    /// </summary>
    public required int Count { get; init; }
}
