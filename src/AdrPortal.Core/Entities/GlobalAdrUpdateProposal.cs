namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents a pending repository-to-library update proposal for a global ADR.
/// </summary>
public sealed class GlobalAdrUpdateProposal
{
    /// <summary>
    /// Gets or sets the unique row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the global ADR identifier.
    /// </summary>
    public Guid GlobalId { get; set; }

    /// <summary>
    /// Gets or sets the repository identifier that submitted the proposal.
    /// </summary>
    public int RepositoryId { get; set; }

    /// <summary>
    /// Gets or sets the local ADR number from the submitting repository.
    /// </summary>
    public int LocalAdrNumber { get; set; }

    /// <summary>
    /// Gets or sets the source template version this proposal is based on.
    /// </summary>
    public int ProposedFromVersion { get; set; }

    /// <summary>
    /// Gets or sets the proposal title.
    /// </summary>
    public required string ProposedTitle { get; set; }

    /// <summary>
    /// Gets or sets the proposed markdown content.
    /// </summary>
    public required string ProposedMarkdownContent { get; set; }

    /// <summary>
    /// Gets or sets whether the proposal is still pending review.
    /// </summary>
    public bool IsPending { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC timestamp when the proposal was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the linked global ADR navigation property.
    /// </summary>
    public GlobalAdr? GlobalAdr { get; set; }

    /// <summary>
    /// Gets or sets the linked managed repository navigation property.
    /// </summary>
    public ManagedRepository? Repository { get; set; }
}
