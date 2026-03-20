using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.State;

namespace AdrPortal.Web.Tests;

public class RepositoryCatalogStateTests
{
    [Test]
    public async Task RefreshAsync_LoadsRepositoriesAndPendingCountAndRaisesChanged()
    {
        var repositories = new[]
        {
            new ManagedRepository
            {
                Id = 1,
                DisplayName = "contoso/repo-a",
                RootPath = @"C:\repos\contoso-repo-a",
                AdrFolder = "docs/adr",
                InboxFolder = "docs/inbox",
                GitRemoteUrl = "https://github.com/contoso/repo-a.git",
                IsActive = true
            },
            new ManagedRepository
            {
                Id = 2,
                DisplayName = "contoso/repo-b",
                RootPath = @"C:\repos\contoso-repo-b",
                AdrFolder = "docs/adr",
                InboxFolder = "docs/inbox",
                GitRemoteUrl = "https://github.com/contoso/repo-b.git",
                IsActive = true
            }
        };

        var managedStore = new FakeManagedRepositoryStore(repositories);
        var globalStore = new FakeGlobalAdrStore(pendingCount: 5);
        var state = new RepositoryCatalogState(managedStore, globalStore);

        var changedCount = 0;
        state.Changed += () => changedCount++;

        await state.RefreshAsync(CancellationToken.None);

        await Assert.That(state.Repositories.Count).IsEqualTo(2);
        await Assert.That(state.Repositories[0].DisplayName).IsEqualTo("contoso/repo-a");
        await Assert.That(state.DashboardPendingCount).IsEqualTo(5);
        await Assert.That(changedCount).IsEqualTo(1);
        await Assert.That(managedStore.GetAllCallCount).IsEqualTo(1);
        await Assert.That(globalStore.GetDashboardPendingCountCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task RefreshAsync_ThrowsWhenCancelledBeforeWork()
    {
        var managedStore = new FakeManagedRepositoryStore([]);
        var globalStore = new FakeGlobalAdrStore(pendingCount: 0);
        var state = new RepositoryCatalogState(managedStore, globalStore);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? exception = null;
        try
        {
            await state.RefreshAsync(cts.Token);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is OperationCanceledException).IsTrue();
        await Assert.That(managedStore.GetAllCallCount).IsEqualTo(0);
        await Assert.That(globalStore.GetDashboardPendingCountCallCount).IsEqualTo(0);
    }

    private sealed class FakeManagedRepositoryStore(IReadOnlyList<ManagedRepository> repositories) : IManagedRepositoryStore
    {
        public int GetAllCallCount { get; private set; }

        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repositories.SingleOrDefault(repository => repository.Id == repositoryId));
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetAllCallCount++;
            return Task.FromResult(repositories);
        }

        public Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repositoryIds);
            ct.ThrowIfCancellationRequested();
            var dictionary = repositories
                .Where(repository => repositoryIds.Contains(repository.Id))
                .ToDictionary(repository => repository.Id);
            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(dictionary);
        }

        public Task<ManagedRepository> AddAsync(ManagedRepository repository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repository);
        }

        public Task<ManagedRepository?> UpdateAsync(ManagedRepository repository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<ManagedRepository?>(repository);
        }

        public Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }

    private sealed class FakeGlobalAdrStore(int pendingCount) : IGlobalAdrStore
    {
        public int GetDashboardPendingCountCallCount { get; private set; }

        public Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<GlobalAdr?>(null);
        }

        public Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdr>>([]);
        }

        public Task<IReadOnlyList<GlobalAdrVersion>> GetVersionsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrVersion>>([]);
        }

        public Task<IReadOnlyList<GlobalAdrUpdateProposal>> GetUpdateProposalsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrUpdateProposal>>([]);
        }

        public Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrInstance>>([]);
        }

        public Task<int> GetDashboardPendingCountAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetDashboardPendingCountCallCount++;
            return Task.FromResult(pendingCount);
        }

        public Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(globalAdr);
        }

        public Task<GlobalAdr?> UpdateAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<GlobalAdr?>(globalAdr);
        }

        public Task<GlobalAdrVersion> AddVersionAsync(GlobalAdrVersion version, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(version);
        }

        public Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(proposal);
        }

        public Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<GlobalAdrUpdateProposal?>(proposal);
        }

        public Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(instance);
        }
    }
}
