using AdrPortal.Core.Entities;
using AdrPortal.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Tests;

public class DatabaseInitializerTests
{
    [Test]
    public async Task InitializeAsync_WhenResetDisabled_PreservesExistingData()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            await using (var seedContext = CreateContext(databasePath))
            {
                await seedContext.Database.MigrateAsync();
                seedContext.ManagedRepositories.Add(CreateRepository("Before Restart"));
                _ = await seedContext.SaveChangesAsync();
            }

            await using (var initializeContext = CreateContext(databasePath))
            {
                await DatabaseInitializer.InitializeAsync(
                    initializeContext,
                    new PersistenceOptions { ResetDatabaseOnStartup = false },
                    CancellationToken.None);
            }

            await using var verifyContext = CreateContext(databasePath);
            var count = await verifyContext.ManagedRepositories.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }
        finally
        {
            DeleteDatabaseArtifacts(databasePath);
        }
    }

    [Test]
    public async Task InitializeAsync_WhenResetEnabled_DeletesExistingData()
    {
        var databasePath = CreateTemporaryDatabasePath();
        try
        {
            await using (var seedContext = CreateContext(databasePath))
            {
                await seedContext.Database.MigrateAsync();
                seedContext.ManagedRepositories.Add(CreateRepository("Before Reset"));
                _ = await seedContext.SaveChangesAsync();
            }

            await using (var initializeContext = CreateContext(databasePath))
            {
                await DatabaseInitializer.InitializeAsync(
                    initializeContext,
                    new PersistenceOptions { ResetDatabaseOnStartup = true },
                    CancellationToken.None);
            }

            await using var verifyContext = CreateContext(databasePath);
            var count = await verifyContext.ManagedRepositories.CountAsync();
            await Assert.That(count).IsEqualTo(0);
        }
        finally
        {
            DeleteDatabaseArtifacts(databasePath);
        }
    }

    private static AdrPortalDbContext CreateContext(string databasePath)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        };

        var options = new DbContextOptionsBuilder<AdrPortalDbContext>()
            .UseSqlite(connectionStringBuilder.ToString())
            .Options;

        return new AdrPortalDbContext(options);
    }

    private static ManagedRepository CreateRepository(string displayName)
    {
        return new ManagedRepository
        {
            DisplayName = displayName,
            RootPath = @"C:\repos\sample",
            AdrFolder = "docs/adr",
            InboxFolder = "docs/adr/inbox",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
    }

    private static string CreateTemporaryDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "adr-database-initializer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "adrportal.db");
    }

    private static void DeleteDatabaseArtifacts(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }
    }
}
