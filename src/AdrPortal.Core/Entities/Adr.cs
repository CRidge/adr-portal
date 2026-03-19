namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents an ADR document parsed from MADR markdown.
/// </summary>
public sealed record Adr
{
    /// <summary>
    /// Gets the sequential ADR number extracted from the filename.
    /// </summary>
    public int Number { get; init; }

    /// <summary>
    /// Gets the URL-friendly slug extracted from the filename.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the repository-relative path to the ADR markdown file.
    /// </summary>
    public required string RepoRelativePath { get; init; }

    /// <summary>
    /// Gets the ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the ADR lifecycle status from MADR front matter.
    /// </summary>
    public AdrStatus Status { get; init; } = AdrStatus.Proposed;

    /// <summary>
    /// Gets the decision date from MADR front matter.
    /// </summary>
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Gets the optional global ADR identifier.
    /// </summary>
    public Guid? GlobalId { get; init; }

    /// <summary>
    /// Gets the optional global template version for cross-repository synchronization.
    /// </summary>
    public int? GlobalVersion { get; init; }

    /// <summary>
    /// Gets the decision makers listed in front matter.
    /// </summary>
    public IReadOnlyList<string> DecisionMakers { get; init; } = [];

    /// <summary>
    /// Gets the consulted participants listed in front matter.
    /// </summary>
    public IReadOnlyList<string> Consulted { get; init; } = [];

    /// <summary>
    /// Gets the informed audience listed in front matter.
    /// </summary>
    public IReadOnlyList<string> Informed { get; init; } = [];

    /// <summary>
    /// Gets the optional ADR number that supersedes this record.
    /// </summary>
    public int? SupersededByNumber { get; init; }

    /// <summary>
    /// Gets the original markdown content.
    /// </summary>
    public string RawMarkdown { get; init; } = string.Empty;
}
