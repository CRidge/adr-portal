using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Workflows;

namespace AdrPortal.Infrastructure.Tests;

public class InMemoryGitPrWorkflowQueueTests
{
    [Test]
    public async Task EnqueueAsync_AndGetByIdAsync_ReturnsQueuedItem()
    {
        var queue = new InMemoryGitPrWorkflowQueue();
        var item = CreateQueueItem(
            id: Guid.NewGuid(),
            enqueuedAtUtc: new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc));

        await queue.EnqueueAsync(item, CancellationToken.None);
        var loaded = await queue.GetByIdAsync(item.Id, CancellationToken.None);

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Id).IsEqualTo(item.Id);
        await Assert.That(queue.Snapshot().Count).IsEqualTo(1);
    }

    [Test]
    public async Task UpsertAsync_ReplacesExistingItemById()
    {
        var queue = new InMemoryGitPrWorkflowQueue();
        var item = CreateQueueItem(
            id: Guid.NewGuid(),
            enqueuedAtUtc: new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc));
        await queue.EnqueueAsync(item, CancellationToken.None);

        var updated = item with
        {
            CommitSha = "abc123",
            PullRequestUrl = new Uri("https://github.com/contoso/adr/pull/5"),
            PullRequestNumber = 5
        };
        await queue.UpsertAsync(updated, CancellationToken.None);

        var loaded = await queue.GetByIdAsync(item.Id, CancellationToken.None);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.CommitSha).IsEqualTo("abc123");
        await Assert.That(loaded.PullRequestNumber).IsEqualTo(5);
        await Assert.That(queue.Snapshot().Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetLatestForAdrAsync_ReturnsNewestItemForRepositoryAdr()
    {
        var queue = new InMemoryGitPrWorkflowQueue();
        var older = CreateQueueItem(
            id: Guid.NewGuid(),
            enqueuedAtUtc: new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc));
        var newer = older with
        {
            Id = Guid.NewGuid(),
            EnqueuedAtUtc = new DateTime(2026, 3, 19, 11, 0, 0, DateTimeKind.Utc),
            CommitSha = "newer"
        };
        await queue.EnqueueAsync(older, CancellationToken.None);
        await queue.EnqueueAsync(newer, CancellationToken.None);

        var latest = await queue.GetLatestForAdrAsync(older.RepositoryId, older.AdrNumber, CancellationToken.None);
        await Assert.That(latest).IsNotNull();
        await Assert.That(latest!.Id).IsEqualTo(newer.Id);
        await Assert.That(latest.CommitSha).IsEqualTo("newer");
    }

    [Test]
    public async Task GetLatestForAdrAsync_ReturnsNullWhenNoItemsExist()
    {
        var queue = new InMemoryGitPrWorkflowQueue();
        var latest = await queue.GetLatestForAdrAsync(repositoryId: 404, adrNumber: 1, CancellationToken.None);
        await Assert.That(latest).IsNull();
    }

    private static GitPrWorkflowQueueItem CreateQueueItem(Guid id, DateTime enqueuedAtUtc)
    {
        return new GitPrWorkflowQueueItem
        {
            Id = id,
            RepositoryId = 100,
            RepositoryDisplayName = "contoso/adr",
            RepositoryRootPath = @"C:\repos\contoso\adr",
            RepositoryRemoteUrl = "https://github.com/contoso/adr.git",
            RepoRelativePath = "docs/adr/adr-0001-test.md",
            AdrNumber = 1,
            AdrSlug = "test",
            AdrTitle = "Test ADR",
            AdrStatus = "Proposed",
            Trigger = GitPrWorkflowTrigger.RepositoryAdrPersist,
            Action = GitPrWorkflowAction.CreateOrUpdatePullRequest,
            BranchName = "proposed/adr-0001-test",
            BaseBranchName = "master",
            EnqueuedAtUtc = enqueuedAtUtc
        };
    }
}
