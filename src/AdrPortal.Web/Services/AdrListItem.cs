using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents an ADR row for list display.
/// </summary>
public sealed record AdrListItem
{
    /// <summary>
    /// Gets the ADR number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the ADR status value.
    /// </summary>
    public required AdrStatus Status { get; init; }

    /// <summary>
    /// Gets the ADR status presentation model.
    /// </summary>
    public required AdrStatusViewModel StatusView { get; init; }

    /// <summary>
    /// Gets the decision date.
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// Gets the ADR slug.
    /// </summary>
    public required string Slug { get; init; }
}
