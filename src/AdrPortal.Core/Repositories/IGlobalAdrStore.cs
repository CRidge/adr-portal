using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Repositories;

/// <summary>
/// Provides persistence operations for the global ADR library and repository instances.
/// </summary>
public interface IGlobalAdrStore
{
    /// <summary>
    /// Gets a global ADR by identifier.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Global ADR when found; otherwise <see langword="null"/>.</returns>
    Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct);

    /// <summary>
    /// Adds a new global ADR topic to the library.
    /// </summary>
    /// <param name="globalAdr">Global ADR values to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted global ADR.</returns>
    Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct);

    /// <summary>
    /// Creates or updates a repository instance linked to a global ADR.
    /// </summary>
    /// <param name="instance">Instance values to create or update.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted instance row.</returns>
    Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct);
}
