using AdrPortal.Core.Entities;
using AdrPortal.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Tests;

public class AdrPortalDbContextModelTests
{
    [Test]
    public async Task ManagedRepositoryModel_MapsToExpectedTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdrPortalDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AdrPortalDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var entityType = context.Model.FindEntityType(typeof(ManagedRepository));

        await Assert.That(entityType).IsNotNull();
        await Assert.That(entityType!.GetTableName()).IsEqualTo("ManagedRepositories");
    }

    [Test]
    public async Task GlobalAdrModel_MapsToExpectedTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdrPortalDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AdrPortalDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var globalAdrEntity = context.Model.FindEntityType(typeof(GlobalAdr));
        var globalAdrInstanceEntity = context.Model.FindEntityType(typeof(GlobalAdrInstance));

        await Assert.That(globalAdrEntity).IsNotNull();
        await Assert.That(globalAdrEntity!.GetTableName()).IsEqualTo("GlobalAdrs");
        await Assert.That(globalAdrInstanceEntity).IsNotNull();
        await Assert.That(globalAdrInstanceEntity!.GetTableName()).IsEqualTo("GlobalAdrInstances");
    }

    [Test]
    public async Task DbContext_ExposesManagedRepositoriesSet()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdrPortalDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AdrPortalDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var repository = new ManagedRepository
        {
            DisplayName = "Repo A",
            RootPath = @"C:\repos\a",
            AdrFolder = "docs/adr",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.ManagedRepositories.Add(repository);
        await context.SaveChangesAsync();

        var storedRepository = await context.ManagedRepositories.SingleAsync();
        await Assert.That(storedRepository.DisplayName).IsEqualTo("Repo A");
        await Assert.That(storedRepository.RootPath).IsEqualTo(@"C:\repos\a");
        await Assert.That(storedRepository.IsActive).IsTrue();
    }
}
