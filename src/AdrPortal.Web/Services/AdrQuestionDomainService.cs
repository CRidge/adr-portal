using System.Text;
using System.Text.RegularExpressions;
using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Generates ADR draft proposals from free-text questions across repository and global-library contexts.
/// </summary>
public sealed class AdrQuestionDomainService(
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IGlobalAdrStore globalAdrStore,
    IAiService aiService)
{
    private static readonly Regex SlugSanitizerRegex = new(
        @"[^a-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int MinimumQuestionLength = 8;
    private const string ManualReviewNotice = "Final option selection and ADR acceptance must be performed manually.";

    /// <summary>
    /// Generates a proposed ADR draft for a repository-scoped question.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="question">Free-text decision question.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Question-driven ADR draft when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<AdrQuestionDraftResult?> GenerateForRepositoryAsync(
        int repositoryId,
        string question,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedQuestion = NormalizeQuestion(question);
        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = madrRepositoryFactory.Create(repository);
        var existingAdrs = await adrRepository.GetAllAsync(ct);
        var acceptedAdrs = existingAdrs
            .Where(adr => adr.Status is AdrStatus.Accepted)
            .OrderBy(adr => adr.Number)
            .ToArray();
        var groundingAdrs = acceptedAdrs.Length > 0
            ? acceptedAdrs
            : existingAdrs.OrderBy(adr => adr.Number).ToArray();
        var contextSummary = BuildRepositoryContextSummary(acceptedAdrs, existingAdrs.Count);

        return await GenerateDraftAsync(
            AdrQuestionContextMode.Repository,
            normalizedQuestion,
            groundingAdrs,
            contextSummary,
            ct);
    }

    /// <summary>
    /// Generates a proposed ADR draft for a global-library-scoped question.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="question">Free-text decision question.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Question-driven ADR draft when global ADR exists; otherwise <see langword="null"/>.</returns>
    public async Task<AdrQuestionDraftResult?> GenerateForGlobalLibraryAsync(
        Guid globalId,
        string question,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedQuestion = NormalizeQuestion(question);
        var globalAdr = await globalAdrStore.GetByIdAsync(globalId, ct);
        if (globalAdr is null)
        {
            return null;
        }

        var versions = await globalAdrStore.GetVersionsAsync(globalId, ct);
        var guidanceAdrs = BuildGlobalGuidanceAdrs(globalAdr, versions);
        var contextSummary = BuildGlobalContextSummary(globalAdr, versions.Count);

        return await GenerateDraftAsync(
            AdrQuestionContextMode.GlobalLibrary,
            normalizedQuestion,
            guidanceAdrs,
            contextSummary,
            ct);
    }

    /// <summary>
    /// Builds a complete question-driven ADR draft result from normalized context.
    /// </summary>
    /// <param name="contextMode">Question context mode.</param>
    /// <param name="question">Normalized free-text question.</param>
    /// <param name="groundingAdrs">ADR corpus used for recommendation grounding.</param>
    /// <param name="contextSummary">Context summary text.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Generated ADR draft result.</returns>
    private async Task<AdrQuestionDraftResult> GenerateDraftAsync(
        AdrQuestionContextMode contextMode,
        string question,
        IReadOnlyList<Adr> groundingAdrs,
        string contextSummary,
        CancellationToken ct)
    {
        var title = BuildTitleFromQuestion(question);
        var slug = NormalizeSlug(title);
        var decisionDrivers = BuildDecisionDrivers(contextMode, contextSummary);
        var seedOptions = BuildSeedOptions(question);
        var problemStatement = BuildProblemStatement(contextMode, question, contextSummary);

        var draftForAnalysis = new AdrDraftForAnalysis
        {
            Title = title,
            Slug = slug,
            ProblemStatement = problemStatement,
            BodyMarkdown = BuildAnalysisBodyMarkdown(problemStatement, decisionDrivers, seedOptions, contextMode, contextSummary),
            DecisionDrivers = decisionDrivers,
            ConsideredOptions = seedOptions
        };

        var recommendation = await aiService.EvaluateAndRecommendAsync(draftForAnalysis, groundingAdrs, ct);
        var consideredOptions = BuildConsideredOptions(recommendation, seedOptions);
        var suggestedOption = ResolveSuggestedOption(recommendation, consideredOptions);
        var suggestedOptionRationale = string.IsNullOrWhiteSpace(recommendation.RecommendationSummary)
            ? "Recommendation rationale requires manual elaboration."
            : recommendation.RecommendationSummary.Trim();
        var draftMarkdownBody = BuildDraftMarkdownBody(
            question,
            problemStatement,
            decisionDrivers,
            consideredOptions,
            suggestedOption,
            suggestedOptionRationale,
            recommendation,
            contextMode,
            contextSummary);

        return new AdrQuestionDraftResult
        {
            ContextMode = contextMode,
            Question = question,
            Title = title,
            Slug = slug,
            ProblemStatement = problemStatement,
            ConsideredOptions = consideredOptions,
            SuggestedOption = suggestedOption,
            SuggestedOptionRationale = suggestedOptionRationale,
            DraftMarkdownBody = draftMarkdownBody,
            DraftStatus = AdrStatus.Proposed,
            Recommendation = recommendation
        };
    }

    /// <summary>
    /// Validates and normalizes a user-provided question value.
    /// </summary>
    /// <param name="question">Question value.</param>
    /// <returns>Normalized question.</returns>
    private static string NormalizeQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question is required.", nameof(question));
        }

        var normalized = question.Trim();
        if (normalized.Length < MinimumQuestionLength)
        {
            throw new ArgumentException(
                $"Question must be at least {MinimumQuestionLength} characters long.",
                nameof(question));
        }

        return normalized;
    }

    /// <summary>
    /// Creates a user-facing ADR title from a free-text question.
    /// </summary>
    /// <param name="question">Normalized question.</param>
    /// <returns>Suggested ADR title.</returns>
    private static string BuildTitleFromQuestion(string question)
    {
        var trimmed = question.Trim().TrimEnd('?', '.', '!', ' ');
        var sentence = string.IsNullOrWhiteSpace(trimmed) ? "Architecture decision" : trimmed;
        var title = $"Decide: {sentence}";
        if (title.Length > 160)
        {
            title = title[..160].TrimEnd();
        }

        return title;
    }

    /// <summary>
    /// Builds a repository mode context summary emphasizing in-force accepted ADRs.
    /// </summary>
    /// <param name="acceptedAdrs">Accepted ADR set.</param>
    /// <param name="totalAdrCount">Total ADR count in the repository.</param>
    /// <returns>Repository context summary.</returns>
    private static string BuildRepositoryContextSummary(IReadOnlyList<Adr> acceptedAdrs, int totalAdrCount)
    {
        if (acceptedAdrs.Count is 0)
        {
            return $"No accepted ADR constraints were found. Use existing repository ADRs ({totalAdrCount}) as background context only.";
        }

        var references = acceptedAdrs
            .Take(5)
            .Select(adr => $"ADR-{adr.Number:0000} '{adr.Title}'")
            .ToArray();
        return $"In-force constraints from accepted ADRs: {string.Join(", ", references)}.";
    }

    /// <summary>
    /// Builds a global mode context summary describing guidance-only behavior.
    /// </summary>
    /// <param name="globalAdr">Global ADR metadata.</param>
    /// <param name="versionCount">Available global version count.</param>
    /// <returns>Global mode context summary.</returns>
    private static string BuildGlobalContextSummary(GlobalAdr globalAdr, int versionCount)
    {
        return $"Global ADR '{globalAdr.Title}' (v{globalAdr.CurrentVersion}) has {versionCount} version snapshot(s). Treat these as guidance only, not mandatory constraints.";
    }

    /// <summary>
    /// Converts global ADR versions into an ADR-like guidance corpus for AI grounding.
    /// </summary>
    /// <param name="globalAdr">Global ADR metadata.</param>
    /// <param name="versions">Global ADR versions.</param>
    /// <returns>Guidance corpus.</returns>
    private static IReadOnlyList<Adr> BuildGlobalGuidanceAdrs(GlobalAdr globalAdr, IReadOnlyList<GlobalAdrVersion> versions)
    {
        var versionEntries = versions
            .OrderByDescending(version => version.VersionNumber)
            .Take(8)
            .Select(
                version => new Adr
                {
                    Number = version.VersionNumber,
                    Slug = $"{NormalizeSlug(version.Title)}-v{version.VersionNumber}",
                    RepoRelativePath = $"global/{globalAdr.GlobalId:D}/v{version.VersionNumber}.md",
                    Title = version.Title,
                    Status = AdrStatus.Proposed,
                    Date = DateOnly.FromDateTime(version.CreatedAtUtc == default ? DateTime.UtcNow : version.CreatedAtUtc),
                    DecisionMakers = ["Global ADR Library"],
                    Consulted = [],
                    Informed = [],
                    RawMarkdown = version.MarkdownContent
                })
            .ToArray();
        if (versionEntries.Length > 0)
        {
            return versionEntries;
        }

        var fallbackVersion = Math.Max(1, globalAdr.CurrentVersion);
        return
        [
            new Adr
            {
                Number = fallbackVersion,
                Slug = $"{NormalizeSlug(globalAdr.Title)}-v{fallbackVersion}",
                RepoRelativePath = $"global/{globalAdr.GlobalId:D}/v{fallbackVersion}.md",
                Title = globalAdr.Title,
                Status = AdrStatus.Proposed,
                Date = DateOnly.FromDateTime(globalAdr.LastUpdatedAtUtc == default ? DateTime.UtcNow : globalAdr.LastUpdatedAtUtc),
                DecisionMakers = ["Global ADR Library"],
                Consulted = [],
                Informed = [],
                RawMarkdown = $"# {globalAdr.Title}\n\nGuidance baseline."
            }
        ];
    }

    /// <summary>
    /// Builds initial considered options from a free-text question.
    /// </summary>
    /// <param name="question">Normalized question.</param>
    /// <returns>Seed options for AI ranking.</returns>
    private static IReadOnlyList<string> BuildSeedOptions(string question)
    {
        var subject = ExtractQuestionSubject(question);
        var candidates = new List<string>
        {
            $"Adopt {subject}",
            $"Retain the current approach for {subject}",
            $"Adopt {subject} incrementally via a pilot rollout",
            $"Use a hybrid strategy for {subject}"
        };

        return candidates
            .Select(candidate => candidate.Trim())
            .Where(candidate => candidate.Length > 10)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
    }

    /// <summary>
    /// Extracts a concise decision subject from a question.
    /// </summary>
    /// <param name="question">Normalized question.</param>
    /// <returns>Decision subject phrase.</returns>
    private static string ExtractQuestionSubject(string question)
    {
        var subject = question.Trim().TrimEnd('?', '.', '!', ' ');
        subject = Regex.Replace(subject, @"\s+", " ", RegexOptions.CultureInvariant);
        if (subject.Length > 100)
        {
            subject = subject[..100].TrimEnd();
        }

        return subject;
    }

    /// <summary>
    /// Builds decision drivers used for AI recommendation grounding.
    /// </summary>
    /// <param name="contextMode">Context mode.</param>
    /// <param name="contextSummary">Mode-specific context summary.</param>
    /// <returns>Decision drivers.</returns>
    private static IReadOnlyList<string> BuildDecisionDrivers(AdrQuestionContextMode contextMode, string contextSummary)
    {
        string[] drivers = contextMode switch
        {
            AdrQuestionContextMode.Repository =>
            [
                "Respect in-force accepted ADRs as active constraints for option evaluation.",
                "Evaluate option trade-offs with explicit pros and cons for manual review.",
                "Keep output in proposed draft state; final acceptance remains a manual workflow step."
            ],
            AdrQuestionContextMode.GlobalLibrary =>
            [
                "Use global ADR versions as guidance and precedent, not as mandatory constraints.",
                "Evaluate option trade-offs with explicit pros and cons for manual review.",
                "Keep output in proposed draft state; final acceptance remains a manual workflow step."
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(contextMode), contextMode, "Unsupported ADR question context mode.")
        };

        return [.. drivers, contextSummary];
    }

    /// <summary>
    /// Builds the problem statement from question and context details.
    /// </summary>
    /// <param name="contextMode">Context mode.</param>
    /// <param name="question">Normalized question.</param>
    /// <param name="contextSummary">Context summary.</param>
    /// <returns>Synthesized problem statement.</returns>
    private static string BuildProblemStatement(AdrQuestionContextMode contextMode, string question, string contextSummary)
    {
        var modeText = contextMode is AdrQuestionContextMode.Repository
            ? "Repository mode requires alignment with accepted ADR constraints."
            : "Global library mode uses existing ADR material as guidance only.";
        return $"{question.TrimEnd('?', ' ')}. {modeText} {contextSummary}";
    }

    /// <summary>
    /// Builds markdown used as AI analysis input.
    /// </summary>
    /// <param name="problemStatement">Problem statement text.</param>
    /// <param name="decisionDrivers">Decision drivers.</param>
    /// <param name="consideredOptions">Seed considered options.</param>
    /// <param name="contextMode">Context mode.</param>
    /// <param name="contextSummary">Context summary.</param>
    /// <returns>Analysis markdown body.</returns>
    private static string BuildAnalysisBodyMarkdown(
        string problemStatement,
        IReadOnlyList<string> decisionDrivers,
        IReadOnlyList<string> consideredOptions,
        AdrQuestionContextMode contextMode,
        string contextSummary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Context and Problem Statement");
        builder.AppendLine();
        builder.AppendLine(problemStatement);
        builder.AppendLine();
        builder.AppendLine($"Context mode: {contextMode}");
        builder.AppendLine(contextSummary);
        builder.AppendLine();
        builder.AppendLine("## Decision Drivers");
        builder.AppendLine();
        foreach (var driver in decisionDrivers)
        {
            builder.AppendLine($"- {driver}");
        }

        builder.AppendLine();
        builder.AppendLine("## Considered Options");
        builder.AppendLine();
        foreach (var option in consideredOptions)
        {
            builder.AppendLine($"- {option}");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Converts recommendation options into ADR editor option view models.
    /// </summary>
    /// <param name="recommendation">AI recommendation output.</param>
    /// <param name="seedOptions">Seed options used when recommendation options are missing.</param>
    /// <returns>Option view model list.</returns>
    private static IReadOnlyList<AdrDecisionOptionViewModel> BuildConsideredOptions(
        AdrEvaluationRecommendation recommendation,
        IReadOnlyList<string> seedOptions)
    {
        var optionsFromRecommendation = recommendation.Options
            .Select(
                option => new AdrDecisionOptionViewModel
                {
                    Name = option.OptionName,
                    Summary = option.Summary,
                    Pros = option.Pros.Count > 0
                        ? option.Pros
                        : ["Potential alignment with documented decision drivers."],
                    Cons = option.Cons.Count > 0
                        ? option.Cons
                        : ["Requires manual validation before implementation commitment."]
                })
            .ToArray();
        if (optionsFromRecommendation.Length > 0)
        {
            return optionsFromRecommendation;
        }

        return seedOptions
            .Select(
                option => new AdrDecisionOptionViewModel
                {
                    Name = option,
                    Summary = "Seed option from question context pending deeper evaluation.",
                    Pros = ["Provides a concrete direction to evaluate."],
                    Cons = ["Requires additional manual analysis to validate fit."]
                })
            .ToArray();
    }

    /// <summary>
    /// Resolves the suggested option value from recommendation and generated options.
    /// </summary>
    /// <param name="recommendation">AI recommendation output.</param>
    /// <param name="consideredOptions">Generated considered options.</param>
    /// <returns>Suggested option value.</returns>
    private static string ResolveSuggestedOption(
        AdrEvaluationRecommendation recommendation,
        IReadOnlyList<AdrDecisionOptionViewModel> consideredOptions)
    {
        if (!string.IsNullOrWhiteSpace(recommendation.PreferredOption))
        {
            return recommendation.PreferredOption.Trim();
        }

        return consideredOptions.Count > 0
            ? consideredOptions[0].Name
            : "No suggested option available.";
    }

    /// <summary>
    /// Builds ADR markdown body from generated question results.
    /// </summary>
    /// <param name="question">Normalized question.</param>
    /// <param name="problemStatement">Problem statement text.</param>
    /// <param name="decisionDrivers">Decision drivers.</param>
    /// <param name="consideredOptions">Structured considered options.</param>
    /// <param name="suggestedOption">Suggested option.</param>
    /// <param name="suggestedOptionRationale">Suggested option rationale.</param>
    /// <param name="recommendation">Raw recommendation output.</param>
    /// <param name="contextMode">Context mode.</param>
    /// <param name="contextSummary">Context summary.</param>
    /// <returns>Draft markdown body.</returns>
    private static string BuildDraftMarkdownBody(
        string question,
        string problemStatement,
        IReadOnlyList<string> decisionDrivers,
        IReadOnlyList<AdrDecisionOptionViewModel> consideredOptions,
        string suggestedOption,
        string suggestedOptionRationale,
        AdrEvaluationRecommendation recommendation,
        AdrQuestionContextMode contextMode,
        string contextSummary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Context and Problem Statement");
        builder.AppendLine();
        builder.AppendLine($"Question: {question}");
        builder.AppendLine(problemStatement);
        builder.AppendLine();
        builder.AppendLine($"Context mode: {contextMode}");
        builder.AppendLine(contextSummary);
        builder.AppendLine();
        builder.AppendLine("## Decision Drivers");
        builder.AppendLine();
        foreach (var driver in decisionDrivers)
        {
            builder.AppendLine($"- {driver}");
        }

        builder.AppendLine();
        builder.AppendLine("## Considered Options");
        builder.AppendLine();
        foreach (var option in consideredOptions)
        {
            builder.AppendLine($"- {option.Name}");
            builder.AppendLine($"  - Summary: {option.Summary}");
            builder.AppendLine("  - Pros:");
            foreach (var pro in option.Pros)
            {
                builder.AppendLine($"    - {pro}");
            }

            builder.AppendLine("  - Cons:");
            foreach (var con in option.Cons)
            {
                builder.AppendLine($"    - {con}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Decision Outcome");
        builder.AppendLine();
        builder.AppendLine($"Chosen option: {suggestedOption}");
        builder.AppendLine();
        builder.AppendLine($"Rationale: {suggestedOptionRationale}");
        builder.AppendLine();
        builder.AppendLine($"Decision fit: {recommendation.DecisionFit}");

        if (recommendation.SuggestedAlternatives.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Rejected alternatives:");
            foreach (var alternative in recommendation.SuggestedAlternatives)
            {
                builder.AppendLine($"- {alternative}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Consequences");
        builder.AppendLine();
        builder.AppendLine("- Positive:");
        builder.AppendLine("  - Generated a structured recommendation with explicit trade-offs.");
        builder.AppendLine("- Negative:");
        builder.AppendLine("  - Requires manual review and consensus before acceptance.");
        builder.AppendLine("- Risks and mitigations:");
        if (recommendation.Risks.Count is 0)
        {
            builder.AppendLine("  - Risk: Explicit risks were not identified.");
            builder.AppendLine("    Mitigation: Perform architecture review before acceptance.");
        }
        else
        {
            foreach (var risk in recommendation.Risks)
            {
                builder.AppendLine($"  - Risk: {risk}");
                builder.AppendLine("    Mitigation: Assign owner and mitigation tasks before acceptance.");
            }
        }

        builder.AppendLine();
        builder.AppendLine(ManualReviewNotice);
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Normalizes ADR title text into a slug.
    /// </summary>
    /// <param name="title">Input title text.</param>
    /// <returns>Normalized lowercase slug.</returns>
    private static string NormalizeSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "adr-question-draft";
        }

        var lowered = title.Trim().ToLowerInvariant();
        var normalized = SlugSanitizerRegex.Replace(lowered, "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "adr-question-draft" : normalized;
    }
}
