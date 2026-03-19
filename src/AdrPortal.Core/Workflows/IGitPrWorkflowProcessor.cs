namespace AdrPortal.Core.Workflows;

/// <summary>
/// Processes queued ADR workflow items with Git and pull request integration.
/// </summary>
public interface IGitPrWorkflowProcessor
{
    /// <summary>
    /// Processes a single queue item and returns the resulting Git/PR state.
    /// </summary>
    /// <param name="item">Queue item to process.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Processing result containing branch, commit, and PR details.</returns>
    Task<GitPrWorkflowProcessingResult> ProcessAsync(GitPrWorkflowQueueItem item, CancellationToken ct);

    /// <summary>
    /// Processes a queue item and updates queue state with resulting Git/PR metadata.
    /// </summary>
    /// <param name="queueItemId">Queue item identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Updated queue item after processing.</returns>
    Task<GitPrWorkflowQueueItem> ProcessAndUpdateQueueAsync(Guid queueItemId, CancellationToken ct);
}
