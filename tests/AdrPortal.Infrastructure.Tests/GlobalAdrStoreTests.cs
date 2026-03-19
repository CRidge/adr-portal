using AdrPortal.Core.Entities;
using AdrPortal.Infrastructure.Data;
using AdrPortal.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Tests;

public class GlobalAdrStoreTests
{
    [Test]
    public async Task AddAsync_PersistsGlobalAdr()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        var globalId = Guid.Parse("7b8f9785-7c8d-4a57-b331-91f0f6fd3dc1");
        var globalAdr = new GlobalAdr
        {
            GlobalId = globalId,
            Title = "Use PostgreSQL",
            CurrentVersion = 1,
            RegisteredAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc)
        };

        await using var context = new AdrPortalDbContext(options);
        var store = new GlobalAdrStore(context);
        var saved = await store.AddAsync(globalAdr, CancellationToken.None);
        var loaded = await context.GlobalAdrs.SingleOrDefaultAsync(item => item.GlobalId == globalId);

        await Assert.That(saved.GlobalId).IsEqualTo(globalId);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Title).IsEqualTo("Use PostgreSQL");
        await Assert.That(loaded.CurrentVersion).IsEqualTo(1);
    }

    [Test]
    public async Task UpsertInstanceAsync_CreatesMapping_WhenMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        var repositoryId = await SeedRepositoryAndGlobalAdrAsync(options);

        var globalId = Guid.Parse("58d38ed8-3b43-4a7e-8bc2-cfae001ba3ce");
        var instance = new GlobalAdrInstance
        {
            GlobalId = globalId,
            RepositoryId = repositoryId,
            LocalAdrNumber = 12,
            RepoRelativePath = "docs/adr/adr-0012-use-vault.md",
            LastKnownStatus = AdrStatus.Accepted,
            BaseTemplateVersion = 3,
            LastReviewedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
        };

        await using var context = new AdrPortalDbContext(options);
        var store = new GlobalAdrStore(context);
        var saved = await store.UpsertInstanceAsync(instance, CancellationToken.None);
        var loaded = await context.GlobalAdrInstances
            .SingleOrDefaultAsync(item => item.GlobalId == globalId && item.RepositoryId == repositoryId && item.LocalAdrNumber == 12);

        await Assert.That(saved.Id > 0).IsTrue();
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.RepoRelativePath).IsEqualTo("docs/adr/adr-0012-use-vault.md");
        await Assert.That(loaded.BaseTemplateVersion).IsEqualTo(3);
        await Assert.That(loaded.LastKnownStatus).IsEqualTo(AdrStatus.Accepted);
    }

    [Test]
    public async Task UpsertInstanceAsync_UpdatesExistingMapping_WhenPresent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        var repositoryId = await SeedRepositoryAndGlobalAdrAsync(options);
        var globalId = Guid.Parse("58d38ed8-3b43-4a7e-8bc2-cfae001ba3ce");

        await using (var seedContext = new AdrPortalDbContext(options))
        {
            seedContext.GlobalAdrInstances.Add(
                new GlobalAdrInstance
                {
                    GlobalId = globalId,
                    RepositoryId = repositoryId,
                    LocalAdrNumber = 12,
                    RepoRelativePath = "docs/adr/adr-0012-use-vault.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 3,
                    LastReviewedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
                });
            _ = await seedContext.SaveChangesAsync();
        }

        var updated = new GlobalAdrInstance
        {
            GlobalId = globalId,
            RepositoryId = repositoryId,
            LocalAdrNumber = 12,
            RepoRelativePath = "docs/adr/adr-0012-use-vault.md",
            LastKnownStatus = AdrStatus.Deprecated,
            BaseTemplateVersion = 4,
            LastReviewedAtUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc)
        };

        await using var context = new AdrPortalDbContext(options);
        var store = new GlobalAdrStore(context);
        var persisted = await store.UpsertInstanceAsync(updated, CancellationToken.None);
        var allRows = await context.GlobalAdrInstances.ToListAsync();

        await Assert.That(allRows.Count).IsEqualTo(1);
        await Assert.That(persisted.BaseTemplateVersion).IsEqualTo(4);
        await Assert.That(persisted.LastKnownStatus).IsEqualTo(AdrStatus.Deprecated);
        await Assert.That(persisted.LastReviewedAtUtc).IsEqualTo(new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNull_WhenGlobalAdrMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        await using var context = new AdrPortalDbContext(options);
        var store = new GlobalAdrStore(context);
        var loaded = await store.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"), CancellationToken.None);

        await Assert.That(loaded).IsNull();
    }

    private static async Task<int> SeedRepositoryAndGlobalAdrAsync(DbContextOptions<AdrPortalDbContext> options)
    {
        await using var setupContext = new AdrPortalDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        var repository = new ManagedRepository
        {
            DisplayName = "Repo One",
            RootPath = @"C:\repos\one",
            AdrFolder = "docs/adr",
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        setupContext.ManagedRepositories.Add(repository);

        setupContext.GlobalAdrs.Add(
            new GlobalAdr
            {
                GlobalId = Guid.Parse("58d38ed8-3b43-4a7e-8bc2-cfae001ba3ce"),
                Title = "Use Vault",
                CurrentVersion = 3,
                RegisteredAtUtc = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc),
                LastUpdatedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
            });

        _ = await setupContext.SaveChangesAsync();
        return repository.Id;
    }

    private static DbContextOptions<AdrPortalDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<AdrPortalDbContext>()
            .UseSqlite(connection)
            .Options;
    }
}
