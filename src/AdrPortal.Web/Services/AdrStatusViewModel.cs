using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Presents ADR status values for UI rendering.
/// </summary>
public sealed record AdrStatusViewModel
{
    /// <summary>
    /// Gets the user-facing label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the CSS modifier class.
    /// </summary>
    public required string CssModifier { get; init; }

    /// <summary>
    /// Gets the uppercase short label used for compact chips.
    /// </summary>
    public required string ShortLabel { get; init; }

    /// <summary>
    /// Creates a view model from a domain status value.
    /// </summary>
    /// <param name="status">ADR status value.</param>
    /// <returns>Mapped status view model.</returns>
    public static AdrStatusViewModel FromStatus(AdrStatus status)
    {
        return status switch
        {
            AdrStatus.Proposed => new AdrStatusViewModel { Label = "Proposed", CssModifier = "proposed", ShortLabel = "PROPOSED" },
            AdrStatus.Accepted => new AdrStatusViewModel { Label = "Accepted", CssModifier = "accepted", ShortLabel = "ACCEPTED" },
            AdrStatus.Rejected => new AdrStatusViewModel { Label = "Rejected", CssModifier = "rejected", ShortLabel = "REJECTED" },
            AdrStatus.Superseded => new AdrStatusViewModel { Label = "Superseded", CssModifier = "superseded", ShortLabel = "SUPERSEDED" },
            AdrStatus.Deprecated => new AdrStatusViewModel { Label = "Deprecated", CssModifier = "deprecated", ShortLabel = "DEPRECATED" },
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported ADR status value.")
        };
    }
}
