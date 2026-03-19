namespace AdrPortal.Core.Workflows;

/// <summary>
/// Provides GitHub pull request operations required by ADR workflow processing.
/// </summary>
public interface IGitHubPullRequestService
{
    /// <summary>
    /// Creates a pull request for a repository branch.
    /// </summary>
    /// <param name="githubToken">GitHub token used for API authentication.</param>
    /// <param name="owner">Repository owner or organization.</param>
    /// <param name="repository">Repository name.</param>
    /// <param name="headBranch">Source branch name.</param>
    /// <param name="baseBranch">Target base branch name.</param>
    /// <param name="title">Pull request title.</param>
    /// <param name="body">Pull request body markdown.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Created pull request URL.</returns>
    Task<Uri> CreatePullRequestAsync(
        string githubToken,
        string owner,
        string repository,
        string headBranch,
        string baseBranch,
        string title,
        string body,
        CancellationToken ct);

    /// <summary>
    /// Merges an existing pull request.
    /// </summary>
    /// <param name="githubToken">GitHub token used for API authentication.</param>
    /// <param name="owner">Repository owner or organization.</param>
    /// <param name="repository">Repository name.</param>
    /// <param name="pullRequestNumber">Pull request number.</param>
    /// <param name="mergeCommitTitle">Optional merge commit title.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    Task MergePullRequestAsync(
        string githubToken,
        string owner,
        string repository,
        int pullRequestNumber,
        string? mergeCommitTitle,
        CancellationToken ct);

    /// <summary>
    /// Closes an existing pull request without merging.
    /// </summary>
    /// <param name="githubToken">GitHub token used for API authentication.</param>
    /// <param name="owner">Repository owner or organization.</param>
    /// <param name="repository">Repository name.</param>
    /// <param name="pullRequestNumber">Pull request number.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    Task ClosePullRequestAsync(
        string githubToken,
        string owner,
        string repository,
        int pullRequestNumber,
        CancellationToken ct);
}
