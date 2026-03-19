using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Provides repository context and persisted ADR data after create or update operations.
/// </summary>
public sealed record AdrPersistResult
{
    /// <summary>
    /// Gets the managed repository where the ADR was persisted.
    /// </summary>
    public required ManagedRepository Repository { get; init; }

    /// <summary>
    /// Gets the persisted ADR domain model.
    /// </summary>
    public required Adr Adr { get; init; }
}
