using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdrPortal.Web.Tests;

public class InboxWatcherCoordinatorTests
{
    [Test]
    public async Task RefreshWatchersAsync_ImportsNewMarkdownFileFromConfiguredInbox()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = new ManagedRepository
            {
                Id = 41,
                DisplayName = "repo-41",
                RootPath = root,
                AdrFolder = "docs/adr",
                InboxFolder = "docs/inbox",
                IsActive = true
            };

            var managedStore = new FakeManagedRepositoryStore([repository]);
            var inboxImportService = new RecordingInboxImportService();
            await using var serviceProvider = CreateServiceProvider(managedStore, inboxImportService);
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var coordinator = new InboxWatcherCoordinator(scopeFactory, NullLogger<InboxWatcherCoordinator>.Instance);
            await coordinator.StartAsync(CancellationToken.None);
            await coordinator.RefreshWatchersAsync(CancellationToken.None);

            var inboxPath = Path.Combine(root, "docs", "inbox");
            Directory.CreateDirectory(inboxPath);
            var filePath = Path.Combine(inboxPath, "watcher.md");
            await File.WriteAllTextAsync(filePath, "# Watcher ADR");

            var imported = await WaitForImportAsync(inboxImportService, timeoutMilliseconds: 4000);

            await Assert.That(imported).IsTrue();
            await Assert.That(inboxImportService.Calls.Count).IsEqualTo(1);
            await Assert.That(inboxImportService.Calls[0].RepositoryId).IsEqualTo(repository.Id);
            await Assert.That(inboxImportService.Calls[0].FilePath.EndsWith("watcher.md", StringComparison.OrdinalIgnoreCase)).IsTrue();

            await coordinator.StopAsync(CancellationToken.None);
            coordinator.Dispose();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task RefreshWatchersAsync_DoesNotWatchWhenInboxIsMissing()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = new ManagedRepository
            {
                Id = 42,
                DisplayName = "repo-42",
                RootPath = root,
                AdrFolder = "docs/adr",
                InboxFolder = null,
                IsActive = true
            };

            var managedStore = new FakeManagedRepositoryStore([repository]);
            var inboxImportService = new RecordingInboxImportService();
            await using var serviceProvider = CreateServiceProvider(managedStore, inboxImportService);
            var coordinator = new InboxWatcherCoordinator(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<InboxWatcherCoordinator>.Instance);

            await coordinator.RefreshWatchersAsync(CancellationToken.None);
            var outsideFilePath = Path.Combine(root, "docs", "inbox", "ignored.md");
            Directory.CreateDirectory(Path.GetDirectoryName(outsideFilePath)!);
            await File.WriteAllTextAsync(outsideFilePath, "# Should Not Import");
            await Task.Delay(500);

            await Assert.That(inboxImportService.Calls.Count).IsEqualTo(0);
            coordinator.Dispose();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    private static async Task<bool> WaitForImportAsync(RecordingInboxImportService inboxImportService, int timeoutMilliseconds)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMilliseconds)
        {
            if (inboxImportService.Calls.Count > 0)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    private static ServiceProvider CreateServiceProvider(
        IManagedRepositoryStore managedRepositoryStore,
        RecordingInboxImportService inboxImportService)
    {
        var services = new ServiceCollection();
        services.AddScoped<IManagedRepositoryStore>(_ => managedRepositoryStore);
        services.AddScoped<IInboxImportService>(_ => inboxImportService);
        return services.BuildServiceProvider();
    }

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "adr-phase10-watcher-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FakeManagedRepositoryStore(IReadOnlyList<ManagedRepository> repositories) : IManagedRepositoryStore
    {
        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repositories.SingleOrDefault(repository => repository.Id == repositoryId));
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
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

    private sealed class RecordingInboxImportService : IInboxImportService
    {
        public List<(int RepositoryId, string FilePath)> Calls { get; } = [];

        public Task<InboxImportResult?> ImportInboxFileAsync(int repositoryId, string inboxFilePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add((repositoryId, inboxFilePath));
            var adr = new Adr
            {
                Number = Calls.Count,
                Slug = "watcher",
                RepoRelativePath = $"docs/adr/adr-{Calls.Count:0000}-watcher.md",
                Title = "Watcher",
                Status = AdrStatus.Proposed,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                RawMarkdown = "# Watcher"
            };

            var repository = new ManagedRepository
            {
                Id = repositoryId,
                DisplayName = "repo",
                RootPath = @"C:\repos\repo",
                AdrFolder = "docs/adr",
                InboxFolder = "docs/inbox",
                IsActive = true
            };

            return Task.FromResult<InboxImportResult?>(new InboxImportResult
            {
                Repository = repository,
                ImportedAdr = adr,
                Message = "ok"
            });
        }

        public Task<InboxImportResult?> ImportInboxMarkdownAsync(int repositoryId, string sourceFileName, string markdown, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<InboxImportResult?>(null);
        }
    }
}
