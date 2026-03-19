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
    /// Gets all global ADR rows, including overview counts.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>All registered global ADR rows.</returns>
    Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Gets all version snapshots for a global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Ordered version rows for the global ADR.</returns>
    Task<IReadOnlyList<GlobalAdrVersion>> GetVersionsAsync(Guid globalId, CancellationToken ct);

    /// <summary>
    /// Gets all update proposals for a global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Proposal rows for the global ADR.</returns>
    Task<IReadOnlyList<GlobalAdrUpdateProposal>> GetUpdateProposalsAsync(Guid globalId, CancellationToken ct);

    /// <summary>
    /// Gets all repository instances for a global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>All linked repository instances.</returns>
    Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct);

    /// <summary>
    /// Gets the total dashboard badge count for pending proposals and update-available instances.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Combined count for global sync notifications.</returns>
    Task<int> GetDashboardPendingCountAsync(CancellationToken ct);

    /// <summary>
    /// Adds a new global ADR topic to the library.
    /// </summary>
    /// <param name="globalAdr">Global ADR values to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted global ADR.</returns>
    Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct);

    /// <summary>
    /// Updates metadata for an existing global ADR row.
    /// </summary>
    /// <param name="globalAdr">Updated global ADR values.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The updated global ADR when found; otherwise <see langword="null"/>.</returns>
    Task<GlobalAdr?> UpdateAsync(GlobalAdr globalAdr, CancellationToken ct);

    /// <summary>
    /// Adds a new immutable version snapshot for a global ADR.
    /// </summary>
    /// <param name="version">Version row to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted version row.</returns>
    Task<GlobalAdrVersion> AddVersionAsync(GlobalAdrVersion version, CancellationToken ct);

    /// <summary>
    /// Adds a new pending update proposal row.
    /// </summary>
    /// <param name="proposal">Proposal row to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted proposal row.</returns>
    Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct);

    /// <summary>
    /// Updates an existing proposal row.
    /// </summary>
    /// <param name="proposal">Updated proposal row.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The updated proposal when found; otherwise <see langword="null"/>.</returns>
    Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct);

    /// <summary>
    /// Creates or updates a repository instance linked to a global ADR.
    /// </summary>
    /// <param name="instance">Instance values to create or update.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted instance row.</returns>
    Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct);
}
