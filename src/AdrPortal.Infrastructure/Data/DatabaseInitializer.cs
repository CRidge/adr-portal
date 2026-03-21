using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Data;

/// <summary>
/// Initializes the ADR Portal database schema during application startup.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Applies startup database initialization behavior.
    /// </summary>
    /// <param name="dbContext">Database context used for initialization.</param>
    /// <param name="persistenceOptions">Persistence options controlling startup behaviors.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task that completes when initialization has finished.</returns>
    public static async Task InitializeAsync(
        AdrPortalDbContext dbContext,
        PersistenceOptions persistenceOptions,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(persistenceOptions);
        ct.ThrowIfCancellationRequested();

        if (persistenceOptions.ResetDatabaseOnStartup)
        {
            _ = await dbContext.Database.EnsureDeletedAsync(ct);
        }

        await dbContext.Database.MigrateAsync(ct);
    }
}
