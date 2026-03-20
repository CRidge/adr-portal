using AdrPortal.Core.Entities;
using AdrPortal.Infrastructure.Data;
using AdrPortal.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Tests;

public class ManagedRepositoryStoreTests
{
    [Test]
    public async Task AddAndGetAll_PersistsManagedRepository()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        await using var context = new AdrPortalDbContext(options);
        var store = new ManagedRepositoryStore(context);

        var saved = await store.AddAsync(
            new ManagedRepository
            {
                DisplayName = "Repo One",
                RootPath = @"C:\repos\one",
                AdrFolder = "docs/adr",
                IsActive = true
            },
            CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);

        await Assert.That(saved.Id > 0).IsTrue();
        await Assert.That(saved.CreatedAtUtc).IsNotEqualTo(default(DateTime));
        await Assert.That(saved.UpdatedAtUtc).IsNotEqualTo(default(DateTime));
        await Assert.That(all.Count).IsEqualTo(1);
        await Assert.That(all[0].DisplayName).IsEqualTo("Repo One");
    }

    [Test]
    public async Task Add_WithUrlOnlyDefaults_PersistsInferredValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        await using var context = new AdrPortalDbContext(options);
        var store = new ManagedRepositoryStore(context);

        var inferredRepository = ManagedRepositoryDefaults.CreateForAdd("https://github.com/contoso/adr-portal.git");
        var saved = await store.AddAsync(inferredRepository, CancellationToken.None);
        var all = await store.GetAllAsync(CancellationToken.None);

        await Assert.That(saved.GitRemoteUrl).IsEqualTo("https://github.com/contoso/adr-portal.git");
        await Assert.That(saved.DisplayName).IsEqualTo("contoso/adr-portal");
        await Assert.That(saved.RootPath).IsEqualTo(Path.Combine(ManagedRepositoryDefaults.DefaultRepositoriesRootPath, "contoso", "adr-portal"));
        await Assert.That(saved.AdrFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultAdrFolder);
        await Assert.That(saved.InboxFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultInboxFolder);
        await Assert.That(all.Count).IsEqualTo(1);
        await Assert.That(all[0].DisplayName).IsEqualTo("contoso/adr-portal");
    }

    [Test]
    public async Task GetById_ReturnsRepositoryWhenFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.ManagedRepositories.Add(
            new ManagedRepository
            {
                DisplayName = "Repo Lookup",
                RootPath = @"C:\repos\lookup",
                AdrFolder = "docs/adr",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
        _ = await setupContext.SaveChangesAsync();
        var id = await setupContext.ManagedRepositories.Select(repository => repository.Id).SingleAsync();

        await using var context = new AdrPortalDbContext(options);
        var store = new ManagedRepositoryStore(context);

        var loaded = await store.GetByIdAsync(id, CancellationToken.None);

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.DisplayName).IsEqualTo("Repo Lookup");
        await Assert.That(loaded.RootPath).IsEqualTo(@"C:\repos\lookup");
    }

    [Test]
    public async Task Update_ChangesStoredValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.ManagedRepositories.Add(
            new ManagedRepository
            {
                DisplayName = "Repo Before",
                RootPath = @"C:\repos\before",
                AdrFolder = "docs/adr",
                IsActive = true,
                CreatedAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc)
            });
        _ = await setupContext.SaveChangesAsync();

        var id = await setupContext.ManagedRepositories.Select(repository => repository.Id).SingleAsync();

        await using var context = new AdrPortalDbContext(options);
        var store = new ManagedRepositoryStore(context);

        var updated = await store.UpdateAsync(
            new ManagedRepository
            {
                Id = id,
                DisplayName = "Repo After",
                RootPath = @"C:\repos\after",
                AdrFolder = "architecture/adr",
                InboxFolder = @"C:\repos\after\inbox",
                GitRemoteUrl = "https://example.test/after.git",
                IsActive = false
            },
            CancellationToken.None);

        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.DisplayName).IsEqualTo("Repo After");
        await Assert.That(updated.RootPath).IsEqualTo(@"C:\repos\after");
        await Assert.That(updated.AdrFolder).IsEqualTo("architecture/adr");
        await Assert.That(updated.IsActive).IsFalse();
        await Assert.That(updated.UpdatedAtUtc > updated.CreatedAtUtc).IsTrue();
    }

    [Test]
    public async Task Delete_RemovesRepositoryFromStore()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.ManagedRepositories.Add(
            new ManagedRepository
            {
                DisplayName = "Repo Remove",
                RootPath = @"C:\repos\remove",
                AdrFolder = "docs/adr",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
            });
        _ = await setupContext.SaveChangesAsync();

        var id = await setupContext.ManagedRepositories.Select(repository => repository.Id).SingleAsync();

        await using var context = new AdrPortalDbContext(options);
        var store = new ManagedRepositoryStore(context);

        var removed = await store.DeleteAsync(id, CancellationToken.None);
        var remaining = await store.GetAllAsync(CancellationToken.None);

        await Assert.That(removed).IsTrue();
        await Assert.That(remaining.Count).IsEqualTo(0);
    }

    private static DbContextOptions<AdrPortalDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<AdrPortalDbContext>()
            .UseSqlite(connection)
            .Options;
    }
}
