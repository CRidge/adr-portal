using AdrPortal.Core.Entities;
using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Workflows;
using Microsoft.Extensions.Options;

namespace AdrPortal.Infrastructure.Tests;

public class GitPrWorkflowProcessorTests
{
    [Test]
    public async Task ProcessAndUpdateQueueAsync_CreateAction_CommitsAndCreatesPullRequest()
    {
        var queueItem = CreateQueueItem(
            action: GitPrWorkflowAction.CreateOrUpdatePullRequest,
            pullRequestNumber: null,
            pullRequestUrl: null,
            commitSha: null);
        var queue = new FakeQueue([queueItem]);
        var credentials = new FakeCredentialResolver();
        var repositoryService = new FakeGitRepositoryService();
        var pullRequestService = new FakeGitHubPullRequestService();
        var processor = CreateProcessor(queue, credentials, repositoryService, pullRequestService);

        var updatedItem = await processor.ProcessAndUpdateQueueAsync(queueItem.Id, CancellationToken.None);

        await Assert.That(repositoryService.CommitCallCount).IsEqualTo(1);
        await Assert.That(pullRequestService.CreateCallCount).IsEqualTo(1);
        await Assert.That(pullRequestService.MergeCallCount).IsEqualTo(0);
        await Assert.That(pullRequestService.CloseCallCount).IsEqualTo(0);
        await Assert.That(updatedItem.CommitSha).IsEqualTo(repositoryService.NextCommitSha);
        await Assert.That(updatedItem.PullRequestNumber).IsEqualTo(5);
        await Assert.That(updatedItem.PullRequestUrl).IsNotNull();
    }

    [Test]
    public async Task ProcessAndUpdateQueueAsync_MergeAction_MergesExistingPullRequest()
    {
        var queueItem = CreateQueueItem(
            action: GitPrWorkflowAction.MergePullRequest,
            pullRequestNumber: 42,
            pullRequestUrl: new Uri("https://github.com/contoso/adr-portal/pull/42"),
            commitSha: "abc123");
        var queue = new FakeQueue([queueItem]);
        var credentials = new FakeCredentialResolver();
        var repositoryService = new FakeGitRepositoryService();
        var pullRequestService = new FakeGitHubPullRequestService();
        var processor = CreateProcessor(queue, credentials, repositoryService, pullRequestService);

        var updatedItem = await processor.ProcessAndUpdateQueueAsync(queueItem.Id, CancellationToken.None);

        await Assert.That(repositoryService.CommitCallCount).IsEqualTo(1);
        await Assert.That(pullRequestService.CreateCallCount).IsEqualTo(0);
        await Assert.That(pullRequestService.MergeCallCount).IsEqualTo(1);
        await Assert.That(pullRequestService.LastMergePullRequestNumber).IsEqualTo(42);
        await Assert.That(updatedItem.PullRequestNumber).IsEqualTo(42);
        await Assert.That(updatedItem.CommitSha).IsEqualTo(repositoryService.NextCommitSha);
    }

    [Test]
    public async Task ProcessAndUpdateQueueAsync_CloseAction_ClosesExistingPullRequest()
    {
        var queueItem = CreateQueueItem(
            action: GitPrWorkflowAction.ClosePullRequest,
            pullRequestNumber: 84,
            pullRequestUrl: new Uri("https://github.com/contoso/adr-portal/pull/84"),
            commitSha: "def456");
        var queue = new FakeQueue([queueItem]);
        var credentials = new FakeCredentialResolver();
        var repositoryService = new FakeGitRepositoryService();
        var pullRequestService = new FakeGitHubPullRequestService();
        var processor = CreateProcessor(queue, credentials, repositoryService, pullRequestService);

        var updatedItem = await processor.ProcessAndUpdateQueueAsync(queueItem.Id, CancellationToken.None);

        await Assert.That(repositoryService.CommitCallCount).IsEqualTo(1);
        await Assert.That(pullRequestService.CreateCallCount).IsEqualTo(0);
        await Assert.That(pullRequestService.CloseCallCount).IsEqualTo(1);
        await Assert.That(pullRequestService.LastClosePullRequestNumber).IsEqualTo(84);
        await Assert.That(updatedItem.PullRequestNumber).IsEqualTo(84);
        await Assert.That(updatedItem.CommitSha).IsEqualTo(repositoryService.NextCommitSha);
    }

    [Test]
    public async Task ProcessAndUpdateQueueAsync_ThrowsWhenQueueItemMissing()
    {
        var queue = new FakeQueue([]);
        var processor = CreateProcessor(
            queue,
            new FakeCredentialResolver(),
            new FakeGitRepositoryService(),
            new FakeGitHubPullRequestService());

        Exception? exception = null;
        try
        {
            _ = await processor.ProcessAndUpdateQueueAsync(Guid.NewGuid(), CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task ProcessAndUpdateQueueAsync_ThrowsWhenMergeHasNoPullRequestNumber()
    {
        var queueItem = CreateQueueItem(
            action: GitPrWorkflowAction.MergePullRequest,
            pullRequestNumber: null,
            pullRequestUrl: null,
            commitSha: "abc123");
        var queue = new FakeQueue([queueItem]);
        var processor = CreateProcessor(
            queue,
            new FakeCredentialResolver(),
            new FakeGitRepositoryService(),
            new FakeGitHubPullRequestService());

        Exception? exception = null;
        try
        {
            _ = await processor.ProcessAndUpdateQueueAsync(queueItem.Id, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("pull request number", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task ProcessAndUpdateQueueAsync_ThrowsWhenRemoteIsNotGitHub()
    {
        var queueItem = CreateQueueItem(
            action: GitPrWorkflowAction.CreateOrUpdatePullRequest,
            pullRequestNumber: null,
            pullRequestUrl: null,
            commitSha: null) with
        {
            RepositoryRemoteUrl = "https://example.com/contoso/adr-portal.git"
        };
        var queue = new FakeQueue([queueItem]);
        var processor = CreateProcessor(
            queue,
            new FakeCredentialResolver(),
            new FakeGitRepositoryService(),
            new FakeGitHubPullRequestService());

        Exception? exception = null;
        try
        {
            _ = await processor.ProcessAndUpdateQueueAsync(queueItem.Id, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("not a GitHub host", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task ProcessAndUpdateQueueAsync_ThrowsWhenTokenIsMissing()
    {
        var queueItem = CreateQueueItem(
            action: GitPrWorkflowAction.CreateOrUpdatePullRequest,
            pullRequestNumber: null,
            pullRequestUrl: null,
            commitSha: null);
        var queue = new FakeQueue([queueItem]);
        var processor = CreateProcessor(
            queue,
            new ThrowingCredentialResolver(),
            new FakeGitRepositoryService(),
            new FakeGitHubPullRequestService());

        Exception? exception = null;
        try
        {
            _ = await processor.ProcessAndUpdateQueueAsync(queueItem.Id, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("GITHUB_TOKEN", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    private static GitPrWorkflowProcessor CreateProcessor(
        IGitPrWorkflowQueue queue,
        IGitCredentialResolver credentialResolver,
        IGitRepositoryService repositoryService,
        IGitHubPullRequestService pullRequestService)
    {
        return new GitPrWorkflowProcessor(
            queue,
            credentialResolver,
            repositoryService,
            pullRequestService,
            Options.Create(new GitHubOptions { DefaultBaseBranch = "master" }));
    }

    private static GitPrWorkflowQueueItem CreateQueueItem(
        GitPrWorkflowAction action,
        int? pullRequestNumber,
        Uri? pullRequestUrl,
        string? commitSha)
    {
        return new GitPrWorkflowQueueItem
        {
            Id = Guid.NewGuid(),
            RepositoryId = 1,
            RepositoryDisplayName = "contoso/adr-portal",
            RepositoryRootPath = @"C:\repos\contoso\adr-portal",
            RepositoryRemoteUrl = "https://github.com/contoso/adr-portal.git",
            RepoRelativePath = "docs/adr/adr-0001-test.md",
            AdrNumber = 1,
            AdrSlug = "test",
            AdrTitle = "Test ADR",
            AdrStatus = "Proposed",
            Trigger = GitPrWorkflowTrigger.RepositoryAdrPersist,
            Action = action,
            BranchName = "proposed/adr-0001-test",
            BaseBranchName = "master",
            PullRequestNumber = pullRequestNumber,
            PullRequestUrl = pullRequestUrl,
            CommitSha = commitSha,
            EnqueuedAtUtc = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class FakeQueue(IEnumerable<GitPrWorkflowQueueItem> seedItems) : IGitPrWorkflowQueue
    {
        private readonly Dictionary<Guid, GitPrWorkflowQueueItem> items = seedItems.ToDictionary(item => item.Id);

        public Task EnqueueAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(item);
            ct.ThrowIfCancellationRequested();
            items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task UpsertAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(item);
            ct.ThrowIfCancellationRequested();
            items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task<GitPrWorkflowQueueItem?> GetLatestForAdrAsync(int repositoryId, int adrNumber, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var latest = items.Values
                .Where(item => item.RepositoryId == repositoryId && item.AdrNumber == adrNumber)
                .OrderByDescending(item => item.EnqueuedAtUtc)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            return Task.FromResult(latest);
        }

        public Task<GitPrWorkflowQueueItem?> GetByIdAsync(Guid queueItemId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = items.TryGetValue(queueItemId, out var item);
            return Task.FromResult(item);
        }
    }

    private sealed class FakeCredentialResolver : IGitCredentialResolver
    {
        public Task<GitCredentialResolution> ResolveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new GitCredentialResolution
                {
                    GitHubToken = "token",
                    GitUserName = "x-access-token",
                    GitPassword = "token"
                });
        }
    }

    private sealed class ThrowingCredentialResolver : IGitCredentialResolver
    {
        public Task<GitCredentialResolution> ResolveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            throw new GitPrWorkflowException("GitHub integration requires GITHUB_TOKEN.");
        }
    }

    private sealed class FakeGitRepositoryService : IGitRepositoryService
    {
        public int CommitCallCount { get; private set; }
        public string NextCommitSha { get; } = "feedbeef";
        public int EnsureRepositoryReadyCallCount { get; private set; }

        public Task EnsureRepositoryReadyAsync(ManagedRepository repository, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ct.ThrowIfCancellationRequested();
            EnsureRepositoryReadyCallCount++;
            return Task.CompletedTask;
        }

        public Task<string> CommitAdrChangeAsync(
            string repositoryRootPath,
            string repoRelativeFilePath,
            string baseBranchName,
            string branchName,
            string commitMessage,
            string? gitUserName,
            string? gitPassword,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CommitCallCount++;
            return Task.FromResult(NextCommitSha);
        }
    }

    private sealed class FakeGitHubPullRequestService : IGitHubPullRequestService
    {
        public int CreateCallCount { get; private set; }
        public int MergeCallCount { get; private set; }
        public int CloseCallCount { get; private set; }
        public int? LastMergePullRequestNumber { get; private set; }
        public int? LastClosePullRequestNumber { get; private set; }

        public Task<Uri> CreatePullRequestAsync(
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
            CreateCallCount++;
            return Task.FromResult(new Uri("https://github.com/contoso/adr-portal/pull/5"));
        }

        public Task MergePullRequestAsync(
            string githubToken,
            string owner,
            string repository,
            int pullRequestNumber,
            string? mergeCommitTitle,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            MergeCallCount++;
            LastMergePullRequestNumber = pullRequestNumber;
            return Task.CompletedTask;
        }

        public Task ClosePullRequestAsync(
            string githubToken,
            string owner,
            string repository,
            int pullRequestNumber,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CloseCallCount++;
            LastClosePullRequestNumber = pullRequestNumber;
            return Task.CompletedTask;
        }
    }
}
