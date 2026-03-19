namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents one global ADR topic registered in the shared library.
/// </summary>
public sealed class GlobalAdr
{
    /// <summary>
    /// Gets or sets the stable global identifier used across repositories.
    /// </summary>
    public Guid GlobalId { get; set; }

    /// <summary>
    /// Gets or sets the latest canonical title for this global ADR.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the current template version in the global library.
    /// </summary>
    public int CurrentVersion { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the ADR was first registered.
    /// </summary>
    public DateTime RegisteredAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the ADR metadata was last updated.
    /// </summary>
    public DateTime LastUpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets repository instances linked to this global ADR.
    /// </summary>
    public ICollection<GlobalAdrInstance> Instances { get; set; } = [];

    /// <summary>
    /// Gets or sets immutable version snapshots for this global ADR.
    /// </summary>
    public ICollection<GlobalAdrVersion> Versions { get; set; } = [];

    /// <summary>
    /// Gets or sets repository-to-library update proposals for this global ADR.
    /// </summary>
    public ICollection<GlobalAdrUpdateProposal> UpdateProposals { get; set; } = [];
}
