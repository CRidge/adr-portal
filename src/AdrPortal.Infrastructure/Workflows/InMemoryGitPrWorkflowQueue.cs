using AdrPortal.Core.Workflows;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Provides a deterministic in-memory queue placeholder for future Git/PR automation workflows.
/// </summary>
public sealed class InMemoryGitPrWorkflowQueue : IGitPrWorkflowQueue
{
    private readonly List<GitPrWorkflowQueueItem> items = [];
    private readonly object gate = new();

    /// <summary>
    /// Enqueues an ADR workflow item for later Git/PR processing.
    /// </summary>
    /// <param name="item">Queue item to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    public Task EnqueueAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();

        lock (gate)
        {
            items.Add(item);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a snapshot of currently queued workflow items.
    /// </summary>
    /// <returns>Queued items captured at call time.</returns>
    public IReadOnlyList<GitPrWorkflowQueueItem> Snapshot()
    {
        lock (gate)
        {
            return items.ToArray();
        }
    }
}
