namespace AdrPortal.Core.Workflows;

/// <summary>
/// Represents a queued repository ADR change awaiting Git and PR automation.
/// </summary>
public sealed record GitPrWorkflowQueueItem
{
    /// <summary>
    /// Gets the managed repository identifier.
    /// </summary>
    public required int RepositoryId { get; init; }

    /// <summary>
    /// Gets the ADR number written to the repository.
    /// </summary>
    public required int AdrNumber { get; init; }

    /// <summary>
    /// Gets the ADR slug used for branch naming.
    /// </summary>
    public required string AdrSlug { get; init; }

    /// <summary>
    /// Gets the ADR title.
    /// </summary>
    public required string AdrTitle { get; init; }

    /// <summary>
    /// Gets the source workflow that generated this queue entry.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the suggested branch name for future Git/PR automation.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the queue item was enqueued.
    /// </summary>
    public required DateTime EnqueuedAtUtc { get; init; }
}
