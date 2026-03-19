using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents the outcome of importing inbox markdown content into an ADR repository.
/// </summary>
public sealed record InboxImportResult
{
    /// <summary>
    /// Gets the repository associated with the import operation.
    /// </summary>
    public required ManagedRepository Repository { get; init; }

    /// <summary>
    /// Gets the persisted ADR created from inbox content.
    /// </summary>
    public required Adr ImportedAdr { get; init; }

    /// <summary>
    /// Gets a summary message suitable for status surfaces.
    /// </summary>
    public required string Message { get; init; }
}
