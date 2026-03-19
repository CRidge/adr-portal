using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents the result of an ADR lifecycle transition operation.
/// </summary>
public sealed record AdrTransitionResult
{
    /// <summary>
    /// Gets the managed repository containing the ADR.
    /// </summary>
    public required ManagedRepository Repository { get; init; }

    /// <summary>
    /// Gets the persisted ADR after transition.
    /// </summary>
    public required Adr Adr { get; init; }

    /// <summary>
    /// Gets a user-facing status message describing the transition outcome.
    /// </summary>
    public required string Message { get; init; }
}
