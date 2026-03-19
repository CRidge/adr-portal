using AdrPortal.Core.Workflows;
using Microsoft.Extensions.Options;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Coordinates queue-driven Git and pull request workflow actions.
/// </summary>
public sealed class GitPrWorkflowProcessor(
    IGitPrWorkflowQueue queue,
    IGitCredentialResolver credentialResolver,
    IGitRepositoryService gitRepositoryService,
    IGitHubPullRequestService gitHubPullRequestService,
    IOptions<GitHubOptions> options) : IGitPrWorkflowProcessor
{
    /// <inheritdoc />
    public async Task<GitPrWorkflowProcessingResult> ProcessAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ct.ThrowIfCancellationRequested();

        var credentials = await credentialResolver.ResolveAsync(ct);
        var repositoryReference = GitHubRemoteUrlParser.Parse(item.RepositoryRemoteUrl);

        return item.Action switch
        {
            GitPrWorkflowAction.CreateOrUpdatePullRequest => await ProcessCreateOrUpdatePullRequestAsync(
                item,
                credentials,
                repositoryReference,
                ct),
            GitPrWorkflowAction.MergePullRequest => await ProcessMergePullRequestAsync(
                item,
                credentials,
                repositoryReference,
                ct),
            GitPrWorkflowAction.ClosePullRequest => await ProcessClosePullRequestAsync(
                item,
                credentials,
                repositoryReference,
                ct),
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.Action, "Unsupported workflow action.")
        };
    }

    /// <inheritdoc />
    public async Task<GitPrWorkflowQueueItem> ProcessAndUpdateQueueAsync(Guid queueItemId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var queueItem = await queue.GetByIdAsync(queueItemId, ct)
            ?? throw new GitPrWorkflowException($"Queue item '{queueItemId}' was not found.");

        var processingResult = await ProcessAsync(queueItem, ct);
        var updatedQueueItem = queueItem with
        {
            BranchName = processingResult.BranchName,
            CommitSha = processingResult.CommitSha ?? queueItem.CommitSha,
            PullRequestUrl = processingResult.PullRequestUrl ?? queueItem.PullRequestUrl,
            PullRequestNumber = processingResult.PullRequestNumber ?? queueItem.PullRequestNumber
        };
        await queue.UpsertAsync(updatedQueueItem, ct);
        return updatedQueueItem;
    }

    private async Task<GitPrWorkflowProcessingResult> ProcessCreateOrUpdatePullRequestAsync(
        GitPrWorkflowQueueItem item,
        GitCredentialResolution credentials,
        GitHubRepositoryReference repositoryReference,
        CancellationToken ct)
    {
        var baseBranch = ResolveBaseBranchName(item.BaseBranchName);
        var commitMessage = $"ADR-{item.AdrNumber:0000}: update {item.AdrTitle}";
        var commitSha = await gitRepositoryService.CommitAdrChangeAsync(
            item.RepositoryRootPath,
            item.RepoRelativePath,
            baseBranch,
            item.BranchName,
            commitMessage,
            credentials.GitUserName,
            credentials.GitPassword,
            ct);

        var pullRequestUrl = item.PullRequestUrl;
        var pullRequestNumber = item.PullRequestNumber;
        if (pullRequestNumber is null)
        {
            var prTitle = $"ADR-{item.AdrNumber:0000}: {item.AdrTitle}";
            var prBody = BuildPullRequestBody(item);
            pullRequestUrl = await gitHubPullRequestService.CreatePullRequestAsync(
                credentials.GitHubToken,
                repositoryReference.Owner,
                repositoryReference.Repository,
                item.BranchName,
                baseBranch,
                prTitle,
                prBody,
                ct);
            pullRequestNumber = TryExtractPullRequestNumber(pullRequestUrl);
        }

        return new GitPrWorkflowProcessingResult
        {
            QueueItemId = item.Id,
            BranchName = item.BranchName,
            CommitSha = commitSha,
            PullRequestUrl = pullRequestUrl,
            PullRequestNumber = pullRequestNumber
        };
    }

    private async Task<GitPrWorkflowProcessingResult> ProcessMergePullRequestAsync(
        GitPrWorkflowQueueItem item,
        GitCredentialResolution credentials,
        GitHubRepositoryReference repositoryReference,
        CancellationToken ct)
    {
        var pullRequestNumber = ResolvePullRequestNumber(item);
        var baseBranch = ResolveBaseBranchName(item.BaseBranchName);
        var commitSha = await gitRepositoryService.CommitAdrChangeAsync(
            item.RepositoryRootPath,
            item.RepoRelativePath,
            baseBranch,
            item.BranchName,
            $"ADR-{item.AdrNumber:0000}: accept {item.AdrTitle}",
            credentials.GitUserName,
            credentials.GitPassword,
            ct);
        await gitHubPullRequestService.MergePullRequestAsync(
            credentials.GitHubToken,
            repositoryReference.Owner,
            repositoryReference.Repository,
            pullRequestNumber,
            $"ADR-{item.AdrNumber:0000}: accept {item.AdrTitle}",
            ct);

        return new GitPrWorkflowProcessingResult
        {
            QueueItemId = item.Id,
            BranchName = item.BranchName,
            CommitSha = commitSha,
            PullRequestUrl = item.PullRequestUrl,
            PullRequestNumber = pullRequestNumber
        };
    }

    private async Task<GitPrWorkflowProcessingResult> ProcessClosePullRequestAsync(
        GitPrWorkflowQueueItem item,
        GitCredentialResolution credentials,
        GitHubRepositoryReference repositoryReference,
        CancellationToken ct)
    {
        var pullRequestNumber = ResolvePullRequestNumber(item);
        var baseBranch = ResolveBaseBranchName(item.BaseBranchName);
        var commitSha = await gitRepositoryService.CommitAdrChangeAsync(
            item.RepositoryRootPath,
            item.RepoRelativePath,
            baseBranch,
            item.BranchName,
            $"ADR-{item.AdrNumber:0000}: reject {item.AdrTitle}",
            credentials.GitUserName,
            credentials.GitPassword,
            ct);
        await gitHubPullRequestService.ClosePullRequestAsync(
            credentials.GitHubToken,
            repositoryReference.Owner,
            repositoryReference.Repository,
            pullRequestNumber,
            ct);

        return new GitPrWorkflowProcessingResult
        {
            QueueItemId = item.Id,
            BranchName = item.BranchName,
            CommitSha = commitSha,
            PullRequestUrl = item.PullRequestUrl,
            PullRequestNumber = pullRequestNumber
        };
    }

    private string ResolveBaseBranchName(string? queueBaseBranch)
    {
        if (!string.IsNullOrWhiteSpace(queueBaseBranch))
        {
            return queueBaseBranch.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Value.DefaultBaseBranch))
        {
            return options.Value.DefaultBaseBranch.Trim();
        }

        return "master";
    }

    private static int ResolvePullRequestNumber(GitPrWorkflowQueueItem item)
    {
        if (item.PullRequestNumber is int explicitNumber && explicitNumber > 0)
        {
            return explicitNumber;
        }

        var parsedNumber = TryExtractPullRequestNumber(item.PullRequestUrl);
        if (parsedNumber is int pullRequestNumber)
        {
            return pullRequestNumber;
        }

        throw new GitPrWorkflowException(
            $"Queue item '{item.Id}' does not have an associated pull request number for action '{item.Action}'.");
    }

    private static int? TryExtractPullRequestNumber(Uri? pullRequestUrl)
    {
        if (pullRequestUrl is null)
        {
            return null;
        }

        var segments = pullRequestUrl.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!string.Equals(segments[index], "pull", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(segments[index + 1], out var pullRequestNumber) && pullRequestNumber > 0)
            {
                return pullRequestNumber;
            }
        }

        return null;
    }

    private static string BuildPullRequestBody(GitPrWorkflowQueueItem item)
    {
        return
            $"Automated ADR workflow update.{Environment.NewLine}{Environment.NewLine}" +
            $"- ADR: {item.AdrNumber:0000} `{item.AdrSlug}`{Environment.NewLine}" +
            $"- Trigger: `{item.Trigger}`{Environment.NewLine}" +
            $"- Action: `{item.Action}`{Environment.NewLine}" +
            $"- Status: `{item.AdrStatus}`";
    }
}
