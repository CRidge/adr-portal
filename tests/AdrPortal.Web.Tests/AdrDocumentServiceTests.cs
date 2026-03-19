using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrDocumentServiceTests
{
    [Test]
    public async Task GetRepositoryWithAdrsAsync_ReturnsRepositoryAndList_WhenRepositoryExists()
    {
        var repository = CreateRepository(id: 42);
        var adrs = new[]
        {
            CreateAdr(number: 1, title: "Use PostgreSQL", slug: "use-postgresql", status: AdrStatus.Accepted),
            CreateAdr(number: 2, title: "Introduce API Gateway Caching", slug: "introduce-api-gateway-caching", status: AdrStatus.Proposed)
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(adrs);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var service = new AdrDocumentService(managedStore, factory);

        var result = await service.GetRepositoryWithAdrsAsync(repository.Id, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Repository.Id).IsEqualTo(42);
        await Assert.That(result.Value.Adrs.Count).IsEqualTo(2);
        await Assert.That(result.Value.Adrs[0].Number).IsEqualTo(1);
        await Assert.That(factory.LastRepositoryId).IsEqualTo(42);
        await Assert.That(fileRepository.GetAllCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetRepositoryWithAdrsAsync_ReturnsNull_WhenRepositoryMissing()
    {
        var managedStore = new FakeManagedRepositoryStore(repository: null);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var service = new AdrDocumentService(managedStore, factory);

        var result = await service.GetRepositoryWithAdrsAsync(repositoryId: 999, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(factory.LastRepositoryId).IsNull();
        await Assert.That(fileRepository.GetAllCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetRepositoryAdrAsync_ReturnsAdr_WhenNumberExists()
    {
        var repository = CreateRepository(id: 7);
        var adrs = new[]
        {
            CreateAdr(number: 12, title: "Deprecate Legacy RPC", slug: "deprecate-legacy-rpc", status: AdrStatus.Deprecated)
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(adrs);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var service = new AdrDocumentService(managedStore, factory);

        var result = await service.GetRepositoryAdrAsync(repositoryId: 7, number: 12, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Repository.Id).IsEqualTo(7);
        await Assert.That(result.Value.Adr.Number).IsEqualTo(12);
        await Assert.That(fileRepository.GetByNumberCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetRepositoryAdrAsync_ReturnsNull_WhenAdrMissing()
    {
        var repository = CreateRepository(id: 8);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var service = new AdrDocumentService(managedStore, factory);

        var result = await service.GetRepositoryAdrAsync(repositoryId: 8, number: 404, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(fileRepository.GetByNumberCallCount).IsEqualTo(1);
    }

    private static ManagedRepository CreateRepository(int id)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = "contoso/adr-portal",
            RootPath = @"C:\repos\contoso\adr-portal",
            AdrFolder = "docs/adr",
            IsActive = true
        };
    }

    private static Adr CreateAdr(int number, string title, string slug, AdrStatus status)
    {
        return new Adr
        {
            Number = number,
            Title = title,
            Slug = slug,
            Status = status,
            Date = new DateOnly(2026, 3, 19),
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            RawMarkdown = $"# {title}"
        };
    }

    private sealed class FakeManagedRepositoryStore(ManagedRepository? repository) : IManagedRepositoryStore
    {
        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (repository is null || repository.Id != repositoryId)
            {
                return Task.FromResult<ManagedRepository?>(null);
            }

            return Task.FromResult<ManagedRepository?>(repository);
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<ManagedRepository> repositories = repository is null ? [] : [repository];
            return Task.FromResult(repositories);
        }

        public Task<ManagedRepository> AddAsync(ManagedRepository addRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(addRepository);
        }

        public Task<ManagedRepository?> UpdateAsync(ManagedRepository updateRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<ManagedRepository?>(updateRepository);
        }

        public Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMadrRepositoryFactory(FakeAdrFileRepository repository) : IMadrRepositoryFactory
    {
        public int? LastRepositoryId { get; private set; }

        public IAdrFileRepository Create(ManagedRepository managedRepository)
        {
            LastRepositoryId = managedRepository.Id;
            return repository;
        }
    }

    private sealed class FakeAdrFileRepository(IReadOnlyList<Adr> adrs) : IAdrFileRepository
    {
        public int GetAllCallCount { get; private set; }
        public int GetByNumberCallCount { get; private set; }

        public Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetAllCallCount++;
            return Task.FromResult(adrs);
        }

        public Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetByNumberCallCount++;
            var adr = adrs.SingleOrDefault(item => item.Number == number);
            return Task.FromResult(adr);
        }

        public Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(adr);
        }

        public Task MoveToRejectedAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<int> GetNextNumberAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var nextNumber = adrs.Count is 0 ? 1 : adrs.Max(item => item.Number) + 1;
            return Task.FromResult(nextNumber);
        }
    }
}
