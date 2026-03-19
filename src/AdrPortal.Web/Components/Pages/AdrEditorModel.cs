using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AdrPortal.Core.Entities;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Components.Pages;

/// <summary>
/// Validated input model for ADR create and edit forms.
/// </summary>
public sealed class AdrEditorModel
{
    private static readonly Regex FrontMatterRegex = new(
        @"\A---\r?\n.*?\r?\n---\r?\n?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Gets or sets the ADR title.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ADR slug.
    /// </summary>
    [Required]
    [StringLength(120)]
    [RegularExpression(@"^[a-z0-9][a-z0-9-]*$", ErrorMessage = "Slug must use lowercase letters, numbers, and hyphens.")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ADR status.
    /// </summary>
    public AdrStatus Status { get; set; } = AdrStatus.Proposed;

    /// <summary>
    /// Gets or sets the ADR decision date.
    /// </summary>
    [Required]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Gets or sets newline or comma-delimited decision makers.
    /// </summary>
    [Required]
    [StringLength(2000)]
    public string DecisionMakersText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets newline or comma-delimited consulted participants.
    /// </summary>
    [StringLength(2000)]
    public string ConsultedText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets newline or comma-delimited informed participants.
    /// </summary>
    [StringLength(2000)]
    public string InformedText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional ADR number that supersedes this ADR.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Superseded-by ADR number must be greater than zero.")]
    public int? SupersededByNumber { get; set; }

    /// <summary>
    /// Gets or sets markdown body content.
    /// </summary>
    [Required]
    public string MarkdownBody { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new editor model pre-populated with MADR template content.
    /// </summary>
    /// <returns>A create-mode editor model.</returns>
    public static AdrEditorModel CreateForNew()
    {
        return new AdrEditorModel
        {
            Status = AdrStatus.Proposed,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MarkdownBody = """
## Context and Problem Statement

## Decision Drivers

## Considered Options

## Decision Outcome

### Consequences

"""
        };
    }

    /// <summary>
    /// Creates an editor model from an ADR domain record.
    /// </summary>
    /// <param name="adr">ADR domain record.</param>
    /// <returns>An edit-mode model seeded with ADR values.</returns>
    public static AdrEditorModel FromAdr(Adr adr)
    {
        ArgumentNullException.ThrowIfNull(adr);

        return new AdrEditorModel
        {
            Title = adr.Title,
            Slug = adr.Slug,
            Status = adr.Status,
            Date = adr.Date,
            DecisionMakersText = JoinValues(adr.DecisionMakers),
            ConsultedText = JoinValues(adr.Consulted),
            InformedText = JoinValues(adr.Informed),
            SupersededByNumber = adr.SupersededByNumber,
            MarkdownBody = ExtractBodyMarkdown(adr.RawMarkdown)
        };
    }

    /// <summary>
    /// Creates a deep copy of the editor state.
    /// </summary>
    /// <returns>Cloned editor model instance.</returns>
    public AdrEditorModel Clone()
    {
        return new AdrEditorModel
        {
            Title = Title,
            Slug = Slug,
            Status = Status,
            Date = Date,
            DecisionMakersText = DecisionMakersText,
            ConsultedText = ConsultedText,
            InformedText = InformedText,
            SupersededByNumber = SupersededByNumber,
            MarkdownBody = MarkdownBody
        };
    }

    /// <summary>
    /// Converts form state into normalized ADR persistence input.
    /// </summary>
    /// <returns>Normalized ADR persistence input.</returns>
    public AdrEditorInput ToInput()
    {
        return new AdrEditorInput
        {
            Title = Title.Trim(),
            Slug = Slug.Trim().ToLowerInvariant(),
            Status = Status,
            Date = Date,
            DecisionMakers = ParsePeople(DecisionMakersText),
            Consulted = ParsePeople(ConsultedText),
            Informed = ParsePeople(InformedText),
            SupersededByNumber = SupersededByNumber,
            BodyMarkdown = MarkdownBody.Trim()
        };
    }

    private static IReadOnlyList<string> ParsePeople(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string JoinValues(IReadOnlyList<string> values)
    {
        return values.Count is 0 ? string.Empty : string.Join(Environment.NewLine, values);
    }

    private static string ExtractBodyMarkdown(string rawMarkdown)
    {
        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return CreateForNew().MarkdownBody;
        }

        var body = FrontMatterRegex.Replace(rawMarkdown.TrimStart('\uFEFF'), string.Empty);
        var trimmedBody = body.TrimStart('\r', '\n');
        return string.IsNullOrWhiteSpace(trimmedBody)
            ? CreateForNew().MarkdownBody
            : trimmedBody;
    }
}
