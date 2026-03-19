namespace AdrPortal.Core.Workflows;

/// <summary>
/// Provides deterministic Git repository operations required by ADR workflow processing.
/// </summary>
public interface IGitRepositoryService
{
    /// <summary>
    /// Stages an ADR file, creates a commit on the target branch, and pushes the branch to origin.
    /// </summary>
    /// <param name="repositoryRootPath">Absolute repository root path.</param>
    /// <param name="repoRelativeFilePath">Repository-relative path to the ADR markdown file.</param>
    /// <param name="baseBranchName">Base branch used when creating the proposal branch.</param>
    /// <param name="branchName">Branch to create or update.</param>
    /// <param name="commitMessage">Commit message for the ADR change.</param>
    /// <param name="gitUserName">Optional Git username for authenticated push.</param>
    /// <param name="gitPassword">Optional Git password or token for authenticated push.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Commit SHA for the created commit.</returns>
    Task<string> CommitAdrChangeAsync(
        string repositoryRootPath,
        string repoRelativeFilePath,
        string baseBranchName,
        string branchName,
        string commitMessage,
        string? gitUserName,
        string? gitPassword,
        CancellationToken ct);
}
