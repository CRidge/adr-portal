namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents a source repository that ADR Portal manages.
/// </summary>
public class ManagedRepository
{
    /// <summary>
    /// Gets or sets the unique identifier for the managed repository.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the absolute path to the repository on disk.
    /// </summary>
    public required string RootPath { get; set; }

    /// <summary>
    /// Gets or sets the relative folder where ADR markdown files are located.
    /// </summary>
    public string AdrFolder { get; set; } = "docs/adr";

    /// <summary>
    /// Gets or sets the inbox folder path used for drop-in ADRs.
    /// </summary>
    public string? InboxFolder { get; set; }

    /// <summary>
    /// Gets or sets the optional Git remote URL for the repository.
    /// </summary>
    public string? GitRemoteUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the repository is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the record was last updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
