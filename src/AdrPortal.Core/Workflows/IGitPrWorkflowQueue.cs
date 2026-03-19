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
}
