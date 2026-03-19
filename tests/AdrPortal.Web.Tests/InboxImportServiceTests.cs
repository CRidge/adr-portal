using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;
using AdrPortal.Infrastructure.Repositories;
using AdrPortal.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdrPortal.Web.Tests;

public class InboxImportServiceTests
{
    [Test]
    public async Task ImportInboxFileAsync_ParsesMarkdownAssignsNextNumberAndDeletesInboxFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository(id: 1, root, adrFolder: "docs/adr", inboxFolder: "docs/inbox");
            var managedStore = new FakeManagedRepositoryStore(repository);
            var madrParser = new MadrParser();
            var madrWriter = new MadrWriter();
            var factory = new MadrRepositoryFactory(madrParser, madrWriter);
            var importService = new InboxImportService(
                managedStore,
                factory,
                madrParser,
                NullLogger<InboxImportService>.Instance);
            var adrRepository = factory.Create(repository);

            _ = await adrRepository.WriteAsync(
                new Adr
                {
                    Number = 1,
                    Slug = "existing-adr",
                    RepoRelativePath = "docs/adr/adr-0001-existing-adr.md",
                    Title = "Existing ADR",
                    Status = AdrStatus.Accepted,
                    Date = new DateOnly(2026, 3, 19),
                    RawMarkdown = "# Existing ADR"
                },
                CancellationToken.None);

            var inboxPath = Path.Combine(root, "docs", "inbox");
            Directory.CreateDirectory(inboxPath);
            var inboxFile = Path.Combine(inboxPath, "proposal.md");
            await File.WriteAllTextAsync(
                inboxFile,
                """
                ---
                status: accepted
                date: 2026-03-22
                decision-makers: [Architecture Board]
                consulted: []
                informed: []
                ---
                # Adopt Eventing

                ## Context and Problem Statement

                Need async communication.
                """);

            var result = await importService.ImportInboxFileAsync(repository.Id, inboxFile, CancellationToken.None);
            var allAdrs = await adrRepository.GetAllAsync(CancellationToken.None);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.ImportedAdr.Number).IsEqualTo(2);
            await Assert.That(result.ImportedAdr.Slug).IsEqualTo("adopt-eventing");
            await Assert.That(result.ImportedAdr.Status).IsEqualTo(AdrStatus.Proposed);
            await Assert.That(result.ImportedAdr.RepoRelativePath).IsEqualTo("docs/adr/adr-0002-adopt-eventing.md");
            await Assert.That(allAdrs.Count).IsEqualTo(2);
            await Assert.That(File.Exists(inboxFile)).IsFalse();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ImportInboxFileAsync_RejectsPathsOutsideInboxRoot()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository(id: 2, root, adrFolder: "docs/adr", inboxFolder: "docs/inbox");
            var managedStore = new FakeManagedRepositoryStore(repository);
            var madrParser = new MadrParser();
            var madrWriter = new MadrWriter();
            var factory = new MadrRepositoryFactory(madrParser, madrWriter);
            var importService = new InboxImportService(
                managedStore,
                factory,
                madrParser,
                NullLogger<InboxImportService>.Instance);
            var outsideFile = Path.Combine(root, "outside.md");
            await File.WriteAllTextAsync(outsideFile, "# Outside");

            Exception? exception = null;
            try
            {
                _ = await importService.ImportInboxFileAsync(repository.Id, outsideFile, CancellationToken.None);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            await Assert.That(exception is InvalidOperationException).IsTrue();
            await Assert.That(exception!.Message.Contains("escapes", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ImportInboxMarkdownAsync_FallsBackWhenMadrParseFails()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository(id: 3, root, adrFolder: "docs/adr", inboxFolder: "docs/inbox");
            var managedStore = new FakeManagedRepositoryStore(repository);
            var madrParser = new MadrParser();
            var madrWriter = new MadrWriter();
            var factory = new MadrRepositoryFactory(madrParser, madrWriter);
            var importService = new InboxImportService(
                managedStore,
                factory,
                madrParser,
                NullLogger<InboxImportService>.Instance);

            var result = await importService.ImportInboxMarkdownAsync(
                repository.Id,
                "freeform.md",
                "# Inbox Decision\n\nNo front matter.",
                CancellationToken.None);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.ImportedAdr.Number).IsEqualTo(1);
            await Assert.That(result.ImportedAdr.Status).IsEqualTo(AdrStatus.Proposed);
            await Assert.That(result.ImportedAdr.Slug).IsEqualTo("inbox-decision");
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "adr-phase10-inbox-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static ManagedRepository CreateRepository(int id, string rootPath, string adrFolder, string inboxFolder)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = $"repo-{id}",
            RootPath = rootPath,
            AdrFolder = adrFolder,
            InboxFolder = inboxFolder,
            IsActive = true
        };
    }

    private sealed class FakeManagedRepositoryStore(ManagedRepository repository) : IManagedRepositoryStore
    {
        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repositoryId == repository.Id ? repository : null);
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ManagedRepository>>([repository]);
        }

        public Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repositoryIds);
            ct.ThrowIfCancellationRequested();
            if (!repositoryIds.Contains(repository.Id))
            {
                return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(new Dictionary<int, ManagedRepository>());
            }

            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(
                new Dictionary<int, ManagedRepository> { [repository.Id] = repository });
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
}
