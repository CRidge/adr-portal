namespace AdrPortal.Core.Workflows;

/// <summary>
/// Represents a queued repository ADR change awaiting Git and PR automation.
/// </summary>
public sealed record GitPrWorkflowQueueItem
{
    /// <summary>
    /// Gets a unique queue item identifier for deterministic processing and tracking.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the managed repository identifier.
    /// </summary>
    public required int RepositoryId { get; init; }

    /// <summary>
    /// Gets the repository display name at queue time.
    /// </summary>
    public required string RepositoryDisplayName { get; init; }

    /// <summary>
    /// Gets the repository root path.
    /// </summary>
    public required string RepositoryRootPath { get; init; }

    /// <summary>
    /// Gets the repository remote URL.
    /// </summary>
    public required string RepositoryRemoteUrl { get; init; }

    /// <summary>
    /// Gets the repository-relative ADR markdown path.
    /// </summary>
    public required string RepoRelativePath { get; init; }

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
    /// Gets the ADR status that initiated this queue action.
    /// </summary>
    public required string AdrStatus { get; init; }

    /// <summary>
    /// Gets the workflow trigger source that generated this queue entry.
    /// </summary>
    public required GitPrWorkflowTrigger Trigger { get; init; }

    /// <summary>
    /// Gets the queue action to perform.
    /// </summary>
    public required GitPrWorkflowAction Action { get; init; }

    /// <summary>
    /// Gets the branch name used for Git and PR operations.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Gets the default branch targeted by pull requests.
    /// </summary>
    public required string BaseBranchName { get; init; }

    /// <summary>
    /// Gets an optional pull request URL associated with this queue item.
    /// </summary>
    public Uri? PullRequestUrl { get; init; }

    /// <summary>
    /// Gets an optional pull request number associated with this queue item.
    /// </summary>
    public int? PullRequestNumber { get; init; }

    /// <summary>
    /// Gets an optional commit SHA associated with this queue item.
    /// </summary>
    public string? CommitSha { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the queue item was enqueued.
    /// </summary>
    public required DateTime EnqueuedAtUtc { get; init; }
}
