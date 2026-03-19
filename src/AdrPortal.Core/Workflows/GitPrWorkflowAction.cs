namespace AdrPortal.Core.Workflows;

/// <summary>
/// Represents ADR workflow actions that require Git/PR automation.
/// </summary>
public enum GitPrWorkflowAction
{
    /// <summary>
    /// Creates or updates a proposal branch commit and opens a pull request.
    /// </summary>
    CreateOrUpdatePullRequest = 0,

    /// <summary>
    /// Merges an existing pull request for a proposal branch.
    /// </summary>
    MergePullRequest = 1,

    /// <summary>
    /// Closes an existing pull request without merging.
    /// </summary>
    ClosePullRequest = 2
}
