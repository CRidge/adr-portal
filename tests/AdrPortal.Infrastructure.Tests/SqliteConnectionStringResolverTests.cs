using AdrPortal.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace AdrPortal.Infrastructure.Tests;

public class SqliteConnectionStringResolverTests
{
    [Test]
    public async Task ResolveConnectionString_RelativeDataSource_NormalizesToConfiguredRoot()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var resolved = SqliteConnectionStringResolver.ResolveConnectionString(
                "Data Source=adrportal.db",
                root,
                Array.Empty<string?>());
            var builder = new SqliteConnectionStringBuilder(resolved);

            await Assert.That(Path.GetFullPath(builder.DataSource)).IsEqualTo(Path.Combine(root, "adrportal.db"));
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ResolveConnectionString_AbsoluteDataSource_LeavesConfiguredStringUntouched()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var absolutePath = Path.Combine(root, "stable", "adrportal.db");
            var configured = $"Data Source={absolutePath};Mode=ReadWriteCreate";
            var resolved = SqliteConnectionStringResolver.ResolveConnectionString(
                configured,
                null,
                Array.Empty<string?>());

            await Assert.That(resolved).IsEqualTo(configured);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ResolveConnectionString_RootTraversal_Throws()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            Exception? exception = null;
            try
            {
                _ = SqliteConnectionStringResolver.ResolveConnectionString(
                    "Data Source=..\\adrportal.db",
                    root,
                    Array.Empty<string?>());
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            await Assert.That(exception is InvalidOperationException).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ResolveConnectionString_RelativeDataSource_MigratesLegacyDatabaseFile()
    {
        var root = CreateTemporaryDirectory();
        var legacyRoot = Path.Combine(root, "legacy");
        var targetRoot = Path.Combine(root, "target");
        Directory.CreateDirectory(legacyRoot);

        var legacyPath = Path.Combine(legacyRoot, "adrportal.db");
        await File.WriteAllTextAsync(legacyPath, "legacy-db");

        var resolved = SqliteConnectionStringResolver.ResolveConnectionString(
            "Data Source=adrportal.db",
            targetRoot,
            legacyRoot);
        var builder = new SqliteConnectionStringBuilder(resolved);
        var targetPath = builder.DataSource;

        try
        {
            await Assert.That(Path.GetFullPath(targetPath)).IsEqualTo(Path.Combine(targetRoot, "adrportal.db"));
            await Assert.That(File.Exists(targetPath)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(targetPath)).IsEqualTo("legacy-db");
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ResolveConnectionString_RelativeDataSource_MigratesLegacyWalAndShmFiles()
    {
        var root = CreateTemporaryDirectory();
        var legacyRoot = Path.Combine(root, "legacy");
        var targetRoot = Path.Combine(root, "target");
        Directory.CreateDirectory(legacyRoot);

        var legacyPath = Path.Combine(legacyRoot, "adrportal.db");
        await File.WriteAllTextAsync(legacyPath, "legacy-db");
        await File.WriteAllTextAsync(legacyPath + "-wal", "legacy-wal");
        await File.WriteAllTextAsync(legacyPath + "-shm", "legacy-shm");

        var resolved = SqliteConnectionStringResolver.ResolveConnectionString(
            "Data Source=adrportal.db",
            targetRoot,
            legacyRoot);
        var builder = new SqliteConnectionStringBuilder(resolved);
        var targetPath = builder.DataSource;

        try
        {
            await Assert.That(File.Exists(targetPath)).IsTrue();
            await Assert.That(File.Exists(targetPath + "-wal")).IsTrue();
            await Assert.That(File.Exists(targetPath + "-shm")).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(targetPath)).IsEqualTo("legacy-db");
            await Assert.That(await File.ReadAllTextAsync(targetPath + "-wal")).IsEqualTo("legacy-wal");
            await Assert.That(await File.ReadAllTextAsync(targetPath + "-shm")).IsEqualTo("legacy-shm");
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task ResolveConnectionString_NoConfiguredRoot_UsesDurableDefaultRoot()
    {
        var resolved = SqliteConnectionStringResolver.ResolveConnectionString(
            "Data Source=adrportal.db",
            null,
            Array.Empty<string?>());
        var builder = new SqliteConnectionStringBuilder(resolved);
        var expectedRoot = SqliteConnectionStringResolver.ResolveDatabaseRootPath(null);
        var expectedPath = Path.Combine(expectedRoot, "adrportal.db");

        await Assert.That(Path.GetFullPath(builder.DataSource)).IsEqualTo(Path.GetFullPath(expectedPath));
    }

    [Test]
    public async Task ResolveDatabaseRootPath_ExplicitRelativeRoot_ResolvesToAbsolutePath()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var resolved = SqliteConnectionStringResolver.ResolveDatabaseRootPath(
                Path.Combine(root, ".", "db-root"));

            await Assert.That(Path.IsPathRooted(resolved)).IsTrue();
            await Assert.That(Directory.Exists(resolved)).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "adr-persistence-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
