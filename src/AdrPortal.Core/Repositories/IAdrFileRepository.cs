using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Repositories;

/// <summary>
/// Provides file-backed ADR persistence operations for a managed repository.
/// </summary>
public interface IAdrFileRepository
{
    /// <summary>
    /// Gets all ADRs from the configured ADR folder.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>All parsed ADRs ordered by number.</returns>
    Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Gets a single ADR by sequential number.
    /// </summary>
    /// <param name="number">ADR number to resolve.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The ADR when found; otherwise <see langword="null"/>.</returns>
    Task<Adr?> GetByNumberAsync(int number, CancellationToken ct);

    /// <summary>
    /// Writes an ADR to disk using MADR format.
    /// </summary>
    /// <param name="adr">ADR metadata and markdown to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted ADR after parsing the saved file content.</returns>
    Task<Adr> WriteAsync(Adr adr, CancellationToken ct);

    /// <summary>
    /// Moves an ADR to the rejected folder and updates status metadata.
    /// </summary>
    /// <param name="number">ADR number to reject.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    Task MoveToRejectedAsync(int number, CancellationToken ct);

    /// <summary>
    /// Gets the next available ADR number based on existing files.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The next sequential ADR number.</returns>
    Task<int> GetNextNumberAsync(CancellationToken ct);
}
