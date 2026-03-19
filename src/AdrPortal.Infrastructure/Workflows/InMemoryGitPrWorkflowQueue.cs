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
            var existingIndex = items.FindIndex(existing => existing.Id == item.Id);
            if (existingIndex >= 0)
            {
                items[existingIndex] = item;
            }
            else
            {
                items.Add(item);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates or updates a queue item snapshot by identifier.
    /// </summary>
    /// <param name="item">Queue item to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    public Task UpsertAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
    {
        return EnqueueAsync(item, ct);
    }

    /// <summary>
    /// Gets the most recent queue item for a repository ADR number.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="adrNumber">ADR number.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Latest queue item when found; otherwise <see langword="null"/>.</returns>
    public Task<GitPrWorkflowQueueItem?> GetLatestForAdrAsync(int repositoryId, int adrNumber, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            var latest = items
                .Where(item => item.RepositoryId == repositoryId && item.AdrNumber == adrNumber)
                .OrderByDescending(item => item.EnqueuedAtUtc)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            return Task.FromResult(latest);
        }
    }

    /// <summary>
    /// Gets a queue item by identifier.
    /// </summary>
    /// <param name="queueItemId">Queue item identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Queue item when found; otherwise <see langword="null"/>.</returns>
    public Task<GitPrWorkflowQueueItem?> GetByIdAsync(Guid queueItemId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            var queueItem = items.FirstOrDefault(item => item.Id == queueItemId);
            return Task.FromResult(queueItem);
        }
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
