using System.Globalization;
using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Coordinates AI-powered ADR evaluation and affected-ADR analysis for editor and detail flows.
/// </summary>
public sealed class AdrAiAssistantService(
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IAiService aiService)
{
    private const string ContextHeading = "## Context and Problem Statement";
    private const string DriversHeading = "## Decision Drivers";
    private const string OptionsHeading = "## Considered Options";

    /// <summary>
    /// Evaluates a draft ADR from editor input and returns a structured recommendation.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="currentAdrNumber">Current ADR number for edit flows, or <see langword="null"/> for create flows.</param>
    /// <param name="draftInput">Normalized draft input to evaluate.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Recommendation when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<AdrEvaluationRecommendation?> EvaluateAndRecommendAsync(
        int repositoryId,
        int? currentAdrNumber,
        AdrEditorInput draftInput,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draftInput);
        ct.ThrowIfCancellationRequested();

        var resolved = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolved is null)
        {
            return null;
        }

        var (_, adrRepository) = resolved.Value;
        var existingAdrs = await adrRepository.GetAllAsync(ct);
        var filteredExistingAdrs = currentAdrNumber is null
            ? existingAdrs
            : existingAdrs.Where(adr => adr.Number != currentAdrNumber.Value).ToArray();
        var draftForAnalysis = BuildDraftForAnalysis(draftInput);
        return await aiService.EvaluateAndRecommendAsync(draftForAnalysis, filteredExistingAdrs, ct);
    }

    /// <summary>
    /// Finds existing ADRs potentially affected by a persisted ADR in repository detail flow.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="adrNumber">ADR number to analyze as the source draft.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Affected ADR analysis when repository and ADR exist; otherwise <see langword="null"/>.</returns>
    public async Task<AffectedAdrAnalysisResult?> FindAffectedAdrsAsync(
        int repositoryId,
        int adrNumber,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resolved = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolved is null)
        {
            return null;
        }

        var (_, adrRepository) = resolved.Value;
        var sourceAdr = await adrRepository.GetByNumberAsync(adrNumber, ct);
        if (sourceAdr is null)
        {
            return null;
        }

        var existingAdrs = await adrRepository.GetAllAsync(ct);
        var filteredExistingAdrs = existingAdrs
            .Where(adr => adr.Number != sourceAdr.Number)
            .ToArray();
        var draftForAnalysis = BuildDraftForAnalysis(sourceAdr);
        return await aiService.FindAffectedAdrsAsync(draftForAnalysis, filteredExistingAdrs, ct);
    }

    /// <summary>
    /// Resolves repository metadata and its ADR file repository.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Repository context when found; otherwise <see langword="null"/>.</returns>
    private async Task<(ManagedRepository Repository, IAdrFileRepository AdrRepository)?> ResolveRepositoryAsync(
        int repositoryId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = madrRepositoryFactory.Create(repository);
        return (repository, adrRepository);
    }

    /// <summary>
    /// Builds analysis input from editor values.
    /// </summary>
    /// <param name="draftInput">Draft editor input.</param>
    /// <returns>Normalized analysis input.</returns>
    private static AdrDraftForAnalysis BuildDraftForAnalysis(AdrEditorInput draftInput)
    {
        var normalizedBody = NormalizeMarkdownBody(draftInput.BodyMarkdown);
        var problemStatement = ExtractProblemStatement(normalizedBody);
        var decisionDrivers = ExtractBulletSection(normalizedBody, DriversHeading);
        var consideredOptions = ExtractBulletSection(normalizedBody, OptionsHeading);

        return new AdrDraftForAnalysis
        {
            Title = draftInput.Title.Trim(),
            Slug = draftInput.Slug.Trim().ToLowerInvariant(),
            ProblemStatement = problemStatement,
            BodyMarkdown = normalizedBody,
            DecisionDrivers = decisionDrivers,
            ConsideredOptions = consideredOptions
        };
    }

    /// <summary>
    /// Builds analysis input from an existing ADR document.
    /// </summary>
    /// <param name="adr">Existing ADR source.</param>
    /// <returns>Normalized analysis input.</returns>
    private static AdrDraftForAnalysis BuildDraftForAnalysis(Adr adr)
    {
        var bodyMarkdown = ExtractBodyMarkdown(adr.RawMarkdown);
        var normalizedBody = NormalizeMarkdownBody(bodyMarkdown);
        var problemStatement = ExtractProblemStatement(normalizedBody);
        var decisionDrivers = ExtractBulletSection(normalizedBody, DriversHeading);
        var consideredOptions = ExtractBulletSection(normalizedBody, OptionsHeading);

        return new AdrDraftForAnalysis
        {
            Title = adr.Title,
            Slug = adr.Slug,
            ProblemStatement = problemStatement,
            BodyMarkdown = normalizedBody,
            DecisionDrivers = decisionDrivers,
            ConsideredOptions = consideredOptions
        };
    }

    /// <summary>
    /// Normalizes markdown body line endings and trims BOM/whitespace.
    /// </summary>
    /// <param name="markdownBody">Markdown content.</param>
    /// <returns>Normalized markdown body.</returns>
    private static string NormalizeMarkdownBody(string markdownBody)
    {
        if (string.IsNullOrWhiteSpace(markdownBody))
        {
            return string.Empty;
        }

        return markdownBody
            .TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    /// <summary>
    /// Removes front matter from persisted ADR markdown and returns body content.
    /// </summary>
    /// <param name="rawMarkdown">Raw ADR markdown including optional front matter.</param>
    /// <returns>Markdown body without front matter fences.</returns>
    private static string ExtractBodyMarkdown(string rawMarkdown)
    {
        var normalized = NormalizeMarkdownBody(rawMarkdown);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lines = SplitLines(normalized);
        if (lines.Count < 3 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            return normalized;
        }

        for (var index = 1; index < lines.Count; index++)
        {
            if (!string.Equals(lines[index].Trim(), "---", StringComparison.Ordinal))
            {
                continue;
            }

            var bodyLines = lines.Skip(index + 1);
            return string.Join('\n', bodyLines).Trim();
        }

        return normalized;
    }

    /// <summary>
    /// Extracts problem statement text from markdown sections.
    /// </summary>
    /// <param name="markdownBody">Normalized markdown body.</param>
    /// <returns>Problem statement text.</returns>
    private static string ExtractProblemStatement(string markdownBody)
    {
        var sectionLines = ExtractSectionLines(markdownBody, ContextHeading);
        if (sectionLines.Count is 0)
        {
            return "No explicit problem statement provided in draft content.";
        }

        return string.Join(' ', sectionLines).Trim();
    }

    /// <summary>
    /// Extracts bullet entries from a named markdown section.
    /// </summary>
    /// <param name="markdownBody">Normalized markdown body.</param>
    /// <param name="sectionHeading">Section heading to inspect.</param>
    /// <returns>Distinct bullet entries.</returns>
    private static IReadOnlyList<string> ExtractBulletSection(string markdownBody, string sectionHeading)
    {
        var sectionLines = ExtractSectionLines(markdownBody, sectionHeading);
        if (sectionLines.Count is 0)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var sectionLine in sectionLines)
        {
            if (!TryParseListItem(sectionLine, out var item))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            items.Add(item.Trim());
        }

        return items
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Extracts lines between a section heading and the next top-level markdown heading.
    /// </summary>
    /// <param name="markdownBody">Normalized markdown body.</param>
    /// <param name="sectionHeading">Heading to locate.</param>
    /// <returns>Trimmed non-empty section lines.</returns>
    private static IReadOnlyList<string> ExtractSectionLines(string markdownBody, string sectionHeading)
    {
        var lines = SplitLines(markdownBody);
        if (lines.Count is 0)
        {
            return [];
        }

        var startIndex = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!string.Equals(lines[index].Trim(), sectionHeading, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            startIndex = index + 1;
            break;
        }

        if (startIndex < 0)
        {
            return [];
        }

        var sectionLines = new List<string>();
        for (var index = startIndex; index < lines.Count; index++)
        {
            var currentLine = lines[index].Trim();
            if (currentLine.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(currentLine))
            {
                continue;
            }

            sectionLines.Add(currentLine);
        }

        return sectionLines;
    }

    /// <summary>
    /// Parses a markdown list item line.
    /// </summary>
    /// <param name="value">Section line value.</param>
    /// <param name="item">Parsed list item value.</param>
    /// <returns><see langword="true"/> when the line is a list item; otherwise <see langword="false"/>.</returns>
    private static bool TryParseListItem(string value, out string item)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            item = trimmed[2..].Trim();
            return true;
        }

        var dotIndex = trimmed.IndexOf(". ", StringComparison.Ordinal);
        if (dotIndex <= 0)
        {
            item = string.Empty;
            return false;
        }

        if (!int.TryParse(trimmed[..dotIndex], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            item = string.Empty;
            return false;
        }

        item = trimmed[(dotIndex + 2)..].Trim();
        return true;
    }

    /// <summary>
    /// Splits normalized markdown into line values.
    /// </summary>
    /// <param name="value">Normalized markdown.</param>
    /// <returns>Line collection.</returns>
    private static IReadOnlyList<string> SplitLines(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split('\n');
    }
}
