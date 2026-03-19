using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Repositories;

/// <summary>
/// Provides CRUD operations for managed repositories persisted by ADR Portal.
/// </summary>
public interface IManagedRepositoryStore
{
    /// <summary>
    /// Gets a managed repository by identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier to resolve.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The managed repository when found; otherwise <see langword="null"/>.</returns>
    Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct);

    /// <summary>
    /// Gets all managed repositories ordered for user display.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Managed repositories from persistence.</returns>
    Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Creates a managed repository record.
    /// </summary>
    /// <param name="repository">Repository details to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted repository including generated identifier values.</returns>
    Task<ManagedRepository> AddAsync(ManagedRepository repository, CancellationToken ct);

    /// <summary>
    /// Updates an existing managed repository record.
    /// </summary>
    /// <param name="repository">Updated repository values.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The updated repository when found; otherwise <see langword="null"/>.</returns>
    Task<ManagedRepository?> UpdateAsync(ManagedRepository repository, CancellationToken ct);

    /// <summary>
    /// Deletes a managed repository record by identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier to remove.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns><see langword="true"/> when a repository was removed; otherwise <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(int repositoryId, CancellationToken ct);
}
