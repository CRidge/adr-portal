using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents a detailed ADR view model for the detail route.
/// </summary>
public sealed record AdrDetailViewModel
{
    /// <summary>
    /// Gets the ADR number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the ADR slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the ADR status value.
    /// </summary>
    public required AdrStatus Status { get; init; }

    /// <summary>
    /// Gets the ADR status presentation model.
    /// </summary>
    public required AdrStatusViewModel StatusView { get; init; }

    /// <summary>
    /// Gets the ADR date.
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// Gets the repository-relative markdown path.
    /// </summary>
    public required string RepoRelativePath { get; init; }

    /// <summary>
    /// Gets the optional global template identifier.
    /// </summary>
    public Guid? GlobalId { get; init; }

    /// <summary>
    /// Gets the optional global template version.
    /// </summary>
    public int? GlobalVersion { get; init; }

    /// <summary>
    /// Gets the optional superseding ADR number.
    /// </summary>
    public int? SupersededByNumber { get; init; }

    /// <summary>
    /// Gets decision makers from front matter.
    /// </summary>
    public required IReadOnlyList<string> DecisionMakers { get; init; }

    /// <summary>
    /// Gets consulted participants from front matter.
    /// </summary>
    public required IReadOnlyList<string> Consulted { get; init; }

    /// <summary>
    /// Gets informed participants from front matter.
    /// </summary>
    public required IReadOnlyList<string> Informed { get; init; }

    /// <summary>
    /// Gets rendered HTML content derived from markdown.
    /// </summary>
    public required string HtmlContent { get; init; }

    /// <summary>
    /// Gets structured MADR sections extracted for richer presentation.
    /// </summary>
    public required AdrStructuredSectionsViewModel StructuredSections { get; init; }
}
