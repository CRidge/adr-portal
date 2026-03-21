namespace AdrPortal.Infrastructure.Data;

/// <summary>
/// Defines persistence settings for ADR Portal storage.
/// </summary>
public sealed class PersistenceOptions
{
    /// <summary>
    /// Configuration section name for persistence options.
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// Gets or sets an optional explicit root directory for SQLite database files.
    /// </summary>
    public string? DatabaseRootPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether startup should delete and recreate the database.
    /// This is destructive and must be explicitly enabled.
    /// </summary>
    public bool ResetDatabaseOnStartup { get; set; }
}
