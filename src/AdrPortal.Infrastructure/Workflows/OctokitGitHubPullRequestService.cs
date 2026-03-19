using AdrPortal.Core.Workflows;
using Octokit;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Executes pull request operations against GitHub using Octokit.
/// </summary>
public sealed class OctokitGitHubPullRequestService : IGitHubPullRequestService
{
    private static GitHubClient CreateClient(string githubToken)
    {
        if (string.IsNullOrWhiteSpace(githubToken))
        {
            throw new GitPrWorkflowException("GitHub token is required for pull request operations.");
        }

        var client = new GitHubClient(new ProductHeaderValue("AdrPortal"));
        client.Credentials = new Credentials(githubToken.Trim());
        return client;
    }

    /// <inheritdoc />
    public async Task<Uri> CreatePullRequestAsync(
        string githubToken,
        string owner,
        string repository,
        string headBranch,
        string baseBranch,
        string title,
        string body,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureRequired(owner, nameof(owner));
        EnsureRequired(repository, nameof(repository));
        EnsureRequired(headBranch, nameof(headBranch));
        EnsureRequired(baseBranch, nameof(baseBranch));
        EnsureRequired(title, nameof(title));

        try
        {
            var client = CreateClient(githubToken);
            var created = await client.PullRequest.Create(
                owner,
                repository,
                new NewPullRequest(title.Trim(), headBranch.Trim(), baseBranch.Trim())
                {
                    Body = body?.Trim()
                });
            if (!Uri.TryCreate(created.HtmlUrl, UriKind.Absolute, out var pullRequestUri))
            {
                throw new GitPrWorkflowException(
                    $"GitHub returned an invalid pull request URL '{created.HtmlUrl}'.");
            }

            return pullRequestUri;
        }
        catch (ApiValidationException exception)
        {
            throw new GitPrWorkflowException($"Unable to create pull request: {exception.ApiError.Message}", exception);
        }
        catch (AuthorizationException exception)
        {
            throw new GitPrWorkflowException(
                "GitHub authentication failed while creating the pull request. Validate GITHUB_TOKEN permissions.",
                exception);
        }
        catch (RateLimitExceededException exception)
        {
            throw new GitPrWorkflowException("GitHub API rate limit exceeded while creating pull request.", exception);
        }
    }

    /// <inheritdoc />
    public async Task MergePullRequestAsync(
        string githubToken,
        string owner,
        string repository,
        int pullRequestNumber,
        string? mergeCommitTitle,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureRequired(owner, nameof(owner));
        EnsureRequired(repository, nameof(repository));
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber), "Pull request number must be greater than zero.");
        }

        try
        {
            var client = CreateClient(githubToken);
            var mergeRequest = new MergePullRequest
            {
                CommitTitle = string.IsNullOrWhiteSpace(mergeCommitTitle) ? null : mergeCommitTitle.Trim()
            };

            var mergeResponse = await client.PullRequest.Merge(owner, repository, pullRequestNumber, mergeRequest);
            if (!mergeResponse.Merged)
            {
                throw new GitPrWorkflowException(
                    $"Pull request #{pullRequestNumber} could not be merged: {mergeResponse.Message ?? "unknown merge failure"}");
            }
        }
        catch (AuthorizationException exception)
        {
            throw new GitPrWorkflowException(
                "GitHub authentication failed while merging pull request. Validate GITHUB_TOKEN permissions.",
                exception);
        }
        catch (ApiValidationException exception)
        {
            throw new GitPrWorkflowException($"Unable to merge pull request: {exception.ApiError.Message}", exception);
        }
    }

    /// <inheritdoc />
    public async Task ClosePullRequestAsync(
        string githubToken,
        string owner,
        string repository,
        int pullRequestNumber,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureRequired(owner, nameof(owner));
        EnsureRequired(repository, nameof(repository));
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber), "Pull request number must be greater than zero.");
        }

        try
        {
            var client = CreateClient(githubToken);
            var update = new PullRequestUpdate { State = ItemState.Closed };
            _ = await client.PullRequest.Update(owner, repository, pullRequestNumber, update);
        }
        catch (AuthorizationException exception)
        {
            throw new GitPrWorkflowException(
                "GitHub authentication failed while closing pull request. Validate GITHUB_TOKEN permissions.",
                exception);
        }
        catch (ApiValidationException exception)
        {
            throw new GitPrWorkflowException($"Unable to close pull request: {exception.ApiError.Message}", exception);
        }
    }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
