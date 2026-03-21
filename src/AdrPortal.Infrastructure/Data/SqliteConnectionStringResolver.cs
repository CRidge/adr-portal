using Microsoft.Data.Sqlite;

namespace AdrPortal.Infrastructure.Data;

/// <summary>
/// Resolves SQLite connection strings to durable file locations.
/// </summary>
internal static class SqliteConnectionStringResolver
{
    private const string AppDataFolderName = "AdrPortal";
    private const string DataFolderName = "data";

    /// <summary>
    /// Resolves a configured SQLite connection string into a runtime-safe form.
    /// </summary>
    /// <param name="configuredConnectionString">Configured connection string.</param>
    /// <param name="configuredDatabaseRootPath">Optional configured root path for relative database files.</param>
    /// <param name="legacySearchRoots">Optional root folders to search for legacy relative database files.</param>
    /// <returns>A normalized connection string with an absolute and durable data source path when applicable.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a relative data source attempts to escape the configured root path.</exception>
    internal static string ResolveConnectionString(
        string configuredConnectionString,
        string? configuredDatabaseRootPath,
        params string?[] legacySearchRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredConnectionString);

        var connectionStringBuilder = new SqliteConnectionStringBuilder(configuredConnectionString);
        if (connectionStringBuilder.Mode is SqliteOpenMode.Memory
            || ShouldKeepConfiguredDataSource(connectionStringBuilder.DataSource))
        {
            return configuredConnectionString;
        }

        var dataSource = connectionStringBuilder.DataSource.Trim();
        if (Path.IsPathRooted(dataSource))
        {
            EnsureContainingDirectoryExists(dataSource);
            return configuredConnectionString;
        }

        var resolvedLegacyPath = ResolveLegacyPath(dataSource, legacySearchRoots);

        var databaseRootPath = ResolveDatabaseRootPath(configuredDatabaseRootPath);
        if (!TryCombineWithinRoot(databaseRootPath, dataSource, out var resolvedDataSource))
        {
            throw new InvalidOperationException(
                $"Relative SQLite data source '{dataSource}' resolves outside configured root '{databaseRootPath}'.");
        }

        MigrateLegacyDatabaseFiles(resolvedLegacyPath, resolvedDataSource);

        connectionStringBuilder.DataSource = resolvedDataSource;
        EnsureContainingDirectoryExists(resolvedDataSource);
        return connectionStringBuilder.ToString();
    }

    /// <summary>
    /// Resolves the durable root folder used for SQLite data files.
    /// </summary>
    /// <param name="configuredDatabaseRootPath">Optional configured root path.</param>
    /// <returns>Absolute path for durable database storage.</returns>
    internal static string ResolveDatabaseRootPath(string? configuredDatabaseRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredDatabaseRootPath))
        {
            var expandedRoot = Environment.ExpandEnvironmentVariables(configuredDatabaseRootPath);
            var explicitRoot = Path.GetFullPath(expandedRoot);
            Directory.CreateDirectory(explicitRoot);
            return explicitRoot;
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            var root = Path.Combine(localApplicationData, AppDataFolderName, DataFolderName);
            Directory.CreateDirectory(root);
            return root;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallbackRoot = string.IsNullOrWhiteSpace(userProfile)
            ? Path.Combine(AppContext.BaseDirectory, ".adrportal", DataFolderName)
            : Path.Combine(userProfile, ".adrportal", DataFolderName);
        Directory.CreateDirectory(fallbackRoot);
        return fallbackRoot;
    }

    /// <summary>
    /// Determines whether a configured data source should be used exactly as provided.
    /// </summary>
    /// <param name="dataSource">SQLite data source value.</param>
    /// <returns><see langword="true"/> when no normalization should be applied; otherwise <see langword="false"/>.</returns>
    private static bool ShouldKeepConfiguredDataSource(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return true;
        }

        if (string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Searches known legacy roots for an existing relative SQLite data file.
    /// </summary>
    /// <param name="relativeDataSource">Relative SQLite data source.</param>
    /// <param name="legacySearchRoots">Candidate legacy search roots.</param>
    /// <returns>Absolute path of the first existing legacy data file; otherwise <see langword="null"/>.</returns>
    private static string? ResolveLegacyPath(
        string relativeDataSource,
        IReadOnlyList<string?>? legacySearchRoots)
    {
        if (legacySearchRoots is null)
        {
            return null;
        }

        foreach (var candidateRoot in legacySearchRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            if (!TryCombineWithinRoot(candidateRoot!, relativeDataSource, out var candidatePath))
            {
                continue;
            }

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    /// <summary>
    /// Copies legacy relative database files into the durable root path when required.
    /// </summary>
    /// <param name="legacyDataSourcePath">Resolved legacy database path when present.</param>
    /// <param name="resolvedDataSource">Resolved durable target data source.</param>
    private static void MigrateLegacyDatabaseFiles(
        string? legacyDataSourcePath,
        string resolvedDataSource)
    {
        if (File.Exists(resolvedDataSource))
        {
            return;
        }

        if (legacyDataSourcePath is null
            || !File.Exists(legacyDataSourcePath)
            || string.Equals(legacyDataSourcePath, resolvedDataSource, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureContainingDirectoryExists(resolvedDataSource);
        File.Copy(legacyDataSourcePath, resolvedDataSource, overwrite: false);
        CopyIfExists(legacyDataSourcePath + "-wal", resolvedDataSource + "-wal");
        CopyIfExists(legacyDataSourcePath + "-shm", resolvedDataSource + "-shm");
    }

    /// <summary>
    /// Combines a root and relative path while enforcing that the result remains under the root.
    /// </summary>
    /// <param name="rootPath">Root directory path.</param>
    /// <param name="relativePath">Relative child path.</param>
    /// <param name="combinedPath">Resolved absolute combined path.</param>
    /// <returns><see langword="true"/> when the combined path remains under the root; otherwise <see langword="false"/>.</returns>
    private static bool TryCombineWithinRoot(string rootPath, string relativePath, out string combinedPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        combinedPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        var normalizedRootWithSeparator = EnsureTrailingSeparator(normalizedRoot);

        return combinedPath.StartsWith(normalizedRootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures that a directory path ends with the current platform directory separator.
    /// </summary>
    /// <param name="path">Directory path to normalize.</param>
    /// <returns>Directory path with trailing separator.</returns>
    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Ensures that the directory containing a file path exists.
    /// </summary>
    /// <param name="filePath">File path whose containing directory should exist.</param>
    private static void EnsureContainingDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Copies a file when the source exists and the target does not yet exist.
    /// </summary>
    /// <param name="sourcePath">Source file path.</param>
    /// <param name="destinationPath">Destination file path.</param>
    private static void CopyIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(destinationPath))
        {
            return;
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
    }
}
