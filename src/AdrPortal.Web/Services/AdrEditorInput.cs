using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Captures normalized user input for creating or updating an ADR.
/// </summary>
public sealed record AdrEditorInput
{
    /// <summary>
    /// Gets the ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the ADR slug used in the markdown filename.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the ADR status value.
    /// </summary>
    public AdrStatus Status { get; init; }

    /// <summary>
    /// Gets the ADR decision date.
    /// </summary>
    public DateOnly Date { get; init; }

    /// <summary>
    /// Gets the ADR decision makers.
    /// </summary>
    public IReadOnlyList<string> DecisionMakers { get; init; } = [];

    /// <summary>
    /// Gets the ADR consulted participants.
    /// </summary>
    public IReadOnlyList<string> Consulted { get; init; } = [];

    /// <summary>
    /// Gets the ADR informed participants.
    /// </summary>
    public IReadOnlyList<string> Informed { get; init; } = [];

    /// <summary>
    /// Gets the optional ADR number that supersedes this ADR.
    /// </summary>
    public int? SupersededByNumber { get; init; }

    /// <summary>
    /// Gets the markdown body content without YAML front matter.
    /// </summary>
    public required string BodyMarkdown { get; init; }
}
