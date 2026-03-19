namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents a repository ADR instance linked to a global ADR topic.
/// </summary>
public sealed class GlobalAdrInstance
{
    /// <summary>
    /// Gets or sets the unique instance identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the linked global ADR identifier.
    /// </summary>
    public Guid GlobalId { get; set; }

    /// <summary>
    /// Gets or sets the linked managed repository identifier.
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// Gets or sets the local ADR number inside the repository.
    /// </summary>
    public int LocalAdrNumber { get; set; }

    /// <summary>
    /// Gets or sets the repository-relative path to the ADR markdown file.
    /// </summary>
    public required string RepoRelativePath { get; set; }

    /// <summary>
    /// Gets or sets the latest known ADR lifecycle status.
    /// </summary>
    public AdrStatus LastKnownStatus { get; set; }

    /// <summary>
    /// Gets or sets the global template version last reviewed for this instance.
    /// </summary>
    public int BaseTemplateVersion { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this instance was last reviewed.
    /// </summary>
    public DateTime LastReviewedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the linked global ADR navigation property.
    /// </summary>
    public GlobalAdr? GlobalAdr { get; set; }

    /// <summary>
    /// Gets or sets the linked managed repository navigation property.
    /// </summary>
    public ManagedRepository? Repository { get; set; }
}
