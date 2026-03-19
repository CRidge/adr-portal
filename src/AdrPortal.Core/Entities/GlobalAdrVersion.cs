namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents an immutable version snapshot for a global ADR template.
/// </summary>
public sealed class GlobalAdrVersion
{
    /// <summary>
    /// Gets or sets the unique row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the owning global ADR identifier.
    /// </summary>
    public Guid GlobalId { get; set; }

    /// <summary>
    /// Gets or sets the sequential version number.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the title recorded for this version.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the markdown snapshot for this version.
    /// </summary>
    public required string MarkdownContent { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this version was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the linked global ADR navigation property.
    /// </summary>
    public GlobalAdr? GlobalAdr { get; set; }
}
