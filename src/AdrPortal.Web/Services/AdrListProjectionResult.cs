namespace AdrPortal.Web.Services;

/// <summary>
/// Represents projected ADR list data with filter metadata.
/// </summary>
public sealed record AdrListProjectionResult
{
    /// <summary>
    /// Gets all list items without filtering applied.
    /// </summary>
    public required IReadOnlyList<AdrListItem> AllItems { get; init; }

    /// <summary>
    /// Gets list items after search and status filters are applied.
    /// </summary>
    public required IReadOnlyList<AdrListItem> FilteredItems { get; init; }

    /// <summary>
    /// Gets status filter options with counts.
    /// </summary>
    public required IReadOnlyList<AdrFilterOption> FilterOptions { get; init; }
}
