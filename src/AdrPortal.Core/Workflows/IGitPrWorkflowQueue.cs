namespace AdrPortal.Core.Workflows;

/// <summary>
/// Defines queue operations for ADR changes awaiting Git and PR integration.
/// </summary>
public interface IGitPrWorkflowQueue
{
    /// <summary>
    /// Enqueues an ADR change for later Git/PR processing.
    /// </summary>
    /// <param name="item">Queue item to add.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    Task EnqueueAsync(GitPrWorkflowQueueItem item, CancellationToken ct);

    /// <summary>
    /// Creates or updates a queue item snapshot by identifier.
    /// </summary>
    /// <param name="item">Queue item to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    Task UpsertAsync(GitPrWorkflowQueueItem item, CancellationToken ct);

    /// <summary>
    /// Gets the most recent queue item for a repository ADR number.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="adrNumber">ADR number.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Latest queue item when found; otherwise <see langword="null"/>.</returns>
    Task<GitPrWorkflowQueueItem?> GetLatestForAdrAsync(int repositoryId, int adrNumber, CancellationToken ct);

    /// <summary>
    /// Gets a queue item by identifier.
    /// </summary>
    /// <param name="queueItemId">Queue item identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Queue item when found; otherwise <see langword="null"/>.</returns>
    Task<GitPrWorkflowQueueItem?> GetByIdAsync(Guid queueItemId, CancellationToken ct);
}
