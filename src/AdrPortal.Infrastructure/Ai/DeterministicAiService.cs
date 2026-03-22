using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AdrPortal.Core.Ai;

namespace AdrPortal.Infrastructure.Ai;

/// <summary>
/// Provides deterministic local ADR bootstrap proposal generation from repository code.
/// </summary>
public sealed class DeterministicAiService : IAiService
{
    private static readonly string[] ManifestPatterns =
    [
        "*.sln",
        "*.slnx",
        "*.csproj",
        "*.fsproj",
        "Directory.Packages.props",
        "Directory.Build.props",
        "global.json",
        "appsettings*.json",
        "package.json",
        "pnpm-lock.yaml",
        "yarn.lock",
        "requirements*.txt",
        "pyproject.toml",
        "go.mod",
        "Dockerfile*",
        "docker-compose*.yml",
        "docker-compose*.yaml",
        "README*"
    ];

    private static readonly string[] SourceFolderNames =
    [
        "src",
        "tests",
        "docs"
    ];

    private static readonly string[] SourceExtensions =
    [
        ".cs",
        ".razor",
        ".js",
        ".ts",
        ".tsx",
        ".json",
        ".yml",
        ".yaml",
        ".md"
    ];

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SlugRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> IgnoredSignalTerms =
    [
        "the",
        "and",
        "for",
        "with",
        "that",
        "this",
        "from",
        "into",
        "using",
        "use",
        "option",
        "options",
        "approach",
        "based",
        "over",
        "under",
        "through",
        "when",
        "where",
        "while",
        "without",
        "within",
        "must",
        "should",
        "could",
        "would",
        "will",
        "not",
        "are",
        "was",
        "were",
        "been",
        "being",
        "have",
        "has",
        "had",
        "more",
        "less",
        "only",
        "also"
    ];
    private const int MaximumFiles = 160;
    private const int MaximumCharactersPerChunk = 1200;
    private const int MaximumPreviewCharacters = 360;
    private const int MinimumRecommendationOptions = 3;
    private const int MaximumRecommendationOptions = 6;

    private sealed record OptionScoreResult
    {
        public required string Option { get; init; }

        public required double Score { get; init; }

        public required IReadOnlyList<string> MatchingDrivers { get; init; }

        public required int ContextSignalHits { get; init; }

        public required IReadOnlyList<int> SupportingAdrNumbers { get; init; }
    }

    /// <summary>
    /// Evaluates a draft ADR and returns deterministic recommendation output.
    /// </summary>
    /// <param name="draftAdr">Draft ADR content under analysis.</param>
    /// <param name="existingAdrs">Existing ADR corpus for grounding.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Deterministic recommendation record.</returns>
    public Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draftAdr);
        ArgumentNullException.ThrowIfNull(existingAdrs);
        ct.ThrowIfCancellationRequested();

        var candidateOptions = BuildCandidateOptions(draftAdr, existingAdrs);
        var rankedOptions = candidateOptions
            .Select(option => ScoreOption(option, existingAdrs, draftAdr))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.MatchingDrivers.Count)
            .ThenBy(item => item.Option, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var preferred = rankedOptions[0];
        var runnerUp = rankedOptions.Length > 1 ? rankedOptions[1] : null;
        var optionRecommendations = rankedOptions
            .Select(
                item => new AdrOptionRecommendation
                {
                    OptionName = item.Option,
                    Summary = BuildOptionSummary(item, draftAdr, existingAdrs),
                    Score = item.Score,
                    Rationale = BuildOptionRationale(item, draftAdr),
                    Pros = BuildOptionPros(item, draftAdr, existingAdrs),
                    Cons = BuildOptionCons(item, draftAdr, existingAdrs),
                    TradeOffs = BuildOptionTradeOffs(item, draftAdr, existingAdrs)
                })
            .ToArray();

        var risks = BuildRecommendationRisks(draftAdr, existingAdrs, preferred, rankedOptions.Length);
        var alternatives = BuildSuggestedAlternatives(preferred.Option, rankedOptions);
        var groundingNumbers = rankedOptions
            .SelectMany(option => option.SupportingAdrNumbers)
            .Concat(existingAdrs.OrderBy(adr => adr.Number).Select(adr => adr.Number))
            .Distinct()
            .Take(8)
            .ToArray();

        var recommendation = new AdrEvaluationRecommendation
        {
            PreferredOption = preferred.Option,
            RecommendationSummary = BuildRecommendationSummary(preferred, runnerUp, draftAdr),
            DecisionFit = BuildDecisionFit(existingAdrs, draftAdr, preferred),
            Options = optionRecommendations,
            Risks = risks,
            SuggestedAlternatives = alternatives,
            GroundingAdrNumbers = groundingNumbers,
            IsFallback = true,
            FallbackReason = "Configured deterministic AI provider."
        };

        return Task.FromResult(recommendation);
    }

    /// <summary>
    /// Generates deterministic ADR draft guidance from a repository question prompt.
    /// </summary>
    /// <param name="question">User-authored ADR question.</param>
    /// <param name="existingAdrs">Existing ADR corpus used as repository constraints.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Generated draft scaffolding and recommendation details.</returns>
    public async Task<AdrQuestionGenerationResult> GenerateDraftFromQuestionAsync(
        string question,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(existingAdrs);
        ct.ThrowIfCancellationRequested();

        var trimmedQuestion = WhitespaceRegex.Replace(question.Trim(), " ");
        var activeConstraints = existingAdrs
            .Where(adr => adr.Status is AdrPortal.Core.Entities.AdrStatus.Proposed or AdrPortal.Core.Entities.AdrStatus.Accepted)
            .OrderBy(adr => adr.Number)
            .ToArray();
        var constraintSet = activeConstraints.Length > 0 ? activeConstraints : existingAdrs.ToArray();
        var suggestedTitle = BuildQuestionTitle(trimmedQuestion);
        var suggestedSlug = NormalizeSlug(suggestedTitle);
        var constraintSummary = BuildConstraintSummary(activeConstraints);
        var problemStatement = BuildQuestionProblemStatement(trimmedQuestion, constraintSummary);
        var decisionDrivers = BuildQuestionDecisionDrivers(trimmedQuestion, constraintSummary);
        var consideredOptions = BuildQuestionSeedOptions(trimmedQuestion, constraintSummary);
        var draft = new AdrDraftForAnalysis
        {
            Title = suggestedTitle,
            Slug = suggestedSlug,
            ProblemStatement = problemStatement,
            BodyMarkdown = BuildQuestionBodyMarkdown(trimmedQuestion, decisionDrivers, consideredOptions),
            DecisionDrivers = decisionDrivers,
            ConsideredOptions = consideredOptions
        };

        var recommendation = await EvaluateAndRecommendAsync(draft, constraintSet, ct);
        return new AdrQuestionGenerationResult
        {
            Question = trimmedQuestion,
            SuggestedTitle = suggestedTitle,
            SuggestedSlug = suggestedSlug,
            ProblemStatement = problemStatement,
            DecisionDrivers = decisionDrivers,
            Recommendation = recommendation
        };
    }

    /// <summary>
    /// Finds affected ADRs using deterministic lexical overlap analysis.
    /// </summary>
    /// <param name="draftAdr">Draft ADR content under analysis.</param>
    /// <param name="existingAdrs">Existing ADR corpus for grounding.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Deterministic affected ADR result set.</returns>
    public Task<AffectedAdrAnalysisResult> FindAffectedAdrsAsync(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draftAdr);
        ArgumentNullException.ThrowIfNull(existingAdrs);
        ct.ThrowIfCancellationRequested();

        var draftTerms = BuildSignalTerms(draftAdr);
        var affectedItems = existingAdrs
            .Select(existing => BuildAffectedItem(existing, draftTerms))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderByDescending(item => item.ImpactLevel)
            .ThenBy(item => item.AdrNumber)
            .Take(10)
            .ToArray();

        var summary = affectedItems.Length is 0
            ? "No directly affected ADRs identified by deterministic lexical overlap."
            : $"Identified {affectedItems.Length} potentially affected ADR(s) using deterministic overlap signals.";

        var result = new AffectedAdrAnalysisResult
        {
            Items = affectedItems,
            Summary = summary,
            IsFallback = true,
            FallbackReason = "Configured deterministic AI provider."
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Scans repository content and returns structured ADR bootstrap proposals.
    /// </summary>
    /// <param name="repoRootPath">Absolute repository root path.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured proposal set derived from local deterministic heuristics.</returns>
    public async Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repoRootPath))
        {
            throw new ArgumentException("Repository root path is required.", nameof(repoRootPath));
        }

        ct.ThrowIfCancellationRequested();

        var normalizedRoot = Path.GetFullPath(repoRootPath);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new DirectoryNotFoundException($"Repository root '{normalizedRoot}' was not found.");
        }

        var selectedFiles = SelectFilesForScan(normalizedRoot);
        var chunks = new List<CodebaseScanChunk>();
        var fileSignals = new Dictionary<string, FileSignal>(StringComparer.OrdinalIgnoreCase);
        var sequence = 0;

        foreach (var filePath in selectedFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(normalizedRoot, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var content = await File.ReadAllTextAsync(filePath, ct);
            var normalized = NormalizeContent(content);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            var truncated = normalized.Length > MaximumCharactersPerChunk
                ? normalized[..MaximumCharactersPerChunk]
                : normalized;
            var preview = truncated.Length > MaximumPreviewCharacters
                ? truncated[..MaximumPreviewCharacters]
                : truncated;

            var chunkId = $"chunk-{++sequence:000}";
            chunks.Add(
                new CodebaseScanChunk
                {
                    ChunkId = chunkId,
                    RepoRelativePath = relativePath,
                    Preview = preview,
                    CharacterCount = truncated.Length
                });

            fileSignals[relativePath] = AnalyzeFile(relativePath, truncated);
        }

        var proposals = BuildProposals(fileSignals, chunks);
        return new AdrBootstrapProposalSet
        {
            RepositoryRootPath = normalizedRoot,
            GeneratedAtUtc = DateTime.UtcNow,
            ScannedFileCount = selectedFiles.Count,
            Chunks = chunks,
            Proposals = proposals
        };
    }

    private static List<string> SelectFilesForScan(string repositoryRootPath)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in ManifestPatterns)
        {
            foreach (var filePath in Directory.EnumerateFiles(repositoryRootPath, pattern, SearchOption.TopDirectoryOnly))
            {
                selected.Add(filePath);
            }
        }

        foreach (var folderName in SourceFolderNames)
        {
            var folderPath = Path.Combine(repositoryRootPath, folderName);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var extension in SourceExtensions)
            {
                foreach (var filePath in Directory.EnumerateFiles(folderPath, $"*{extension}", SearchOption.AllDirectories))
                {
                    selected.Add(filePath);
                }
            }
        }

        return selected
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFiles)
            .ToList();
    }

    private static string NormalizeContent(string content)
    {
        var trimmed = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        return WhitespaceRegex.Replace(trimmed, " ");
    }

    private static FileSignal AnalyzeFile(string relativePath, string content)
    {
        return new FileSignal
        {
            RelativePath = relativePath,
            ContainsAspire = ContainsAny(content, "aspire", "DistributedApplication", "WithExternalHttpEndpoints"),
            ContainsEfCore = ContainsAny(content, "EntityFrameworkCore", "DbContext", "migrate", "sqlite"),
            ContainsTesting = ContainsAny(content, "TUnit", "[Test]", "dotnet test"),
            ContainsSecurity = ContainsAny(content, "Authentication", "Authorization", "Jwt", "https", "Antiforgery"),
            ContainsObservability = ContainsAny(content, "OpenTelemetry", "ServiceDefaults", "health", "trace")
        };
    }

    private static bool ContainsAny(string content, params string[] values)
    {
        return values.Any(value => content.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> BuildCandidateOptions(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs)
    {
        var options = new List<string>();
        foreach (var consideredOption in draftAdr.ConsideredOptions)
        {
            AddOptionCandidate(options, consideredOption);
        }

        var scope = ResolveDecisionScope(draftAdr);
        var primaryDriver = ShortenForSentence(
            draftAdr.DecisionDrivers.FirstOrDefault(),
            "the primary decision driver");
        var secondaryDriver = ShortenForSentence(
            draftAdr.DecisionDrivers.Skip(1).FirstOrDefault(),
            "cross-team consistency");
        var acceptedReference = existingAdrs
            .Where(adr => adr.Status is AdrPortal.Core.Entities.AdrStatus.Accepted)
            .OrderBy(adr => adr.Number)
            .Select(adr => adr.Title)
            .FirstOrDefault();

        AddOptionCandidate(options, $"Standardize {scope} around {primaryDriver}");
        AddOptionCandidate(options, $"Keep current {scope} approach with targeted improvements");
        AddOptionCandidate(options, $"Adopt a phased rollout for {scope} with measurable checkpoints");
        AddOptionCandidate(options, $"Blend {scope} choices to balance {primaryDriver} and {secondaryDriver}");
        if (!string.IsNullOrWhiteSpace(acceptedReference))
        {
            AddOptionCandidate(options, $"Align {scope} with precedent from '{ShortenForSentence(acceptedReference, acceptedReference)}'");
        }

        AddOptionCandidate(options, $"Delay final {scope} commitment until additional evidence is collected");

        if (options.Count < MinimumRecommendationOptions)
        {
            AddOptionCandidate(options, $"Adopt a platform-default baseline for {scope}");
            AddOptionCandidate(options, $"Continue current {scope} decision with explicit guardrails");
            AddOptionCandidate(options, $"Run a short pilot before finalizing {scope}");
        }

        return options
            .Take(MaximumRecommendationOptions)
            .ToArray();
    }

    private static string BuildQuestionTitle(string question)
    {
        var withoutQuestionPrefix = question.StartsWith("Should ", StringComparison.OrdinalIgnoreCase)
            ? question["Should ".Length..]
            : question;
        var withoutPunctuation = withoutQuestionPrefix
            .Trim()
            .TrimEnd('?', '.', '!');
        if (string.IsNullOrWhiteSpace(withoutPunctuation))
        {
            return "Answer repository architecture question";
        }

        var normalized = WhitespaceRegex.Replace(withoutPunctuation, " ");
        var sentenceCase = char.ToUpperInvariant(normalized[0]) + normalized[1..];
        return $"Decide how to {ShortenForSentence(sentenceCase, sentenceCase)}";
    }

    private static string BuildQuestionProblemStatement(string question, string constraintSummary)
    {
        var constraintClause = string.IsNullOrWhiteSpace(constraintSummary)
            ? "No active ADR constraints were detected."
            : constraintSummary;
        return $"The repository needs a documented architecture decision to answer: \"{question}\". {constraintClause}";
    }

    private static IReadOnlyList<string> BuildQuestionDecisionDrivers(string question, string constraintSummary)
    {
        var drivers = new List<string>
        {
            $"Provide a clear answer to the repository question: \"{question}\"",
            "Capture options, pros, cons, and rationale in a reviewable proposed ADR",
            "Keep rollout risk explicit before any acceptance action"
        };
        if (!string.IsNullOrWhiteSpace(constraintSummary))
        {
            drivers.Add(constraintSummary);
        }

        return drivers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildQuestionSeedOptions(string question, string constraintSummary)
    {
        var options = new List<string>
        {
            $"Standardize on a single repository-wide approach to \"{question}\"",
            $"Adopt an incremental pilot path to answer \"{question}\"",
            $"Retain the current approach while adding explicit guardrails for \"{question}\""
        };
        if (!string.IsNullOrWhiteSpace(constraintSummary))
        {
            options.Add("Align to active ADR constraints with targeted exceptions only when justified");
        }

        return options
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRecommendationOptions)
            .ToArray();
    }

    private static string BuildQuestionBodyMarkdown(
        string question,
        IReadOnlyList<string> decisionDrivers,
        IReadOnlyList<string> consideredOptions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Context and Problem Statement");
        builder.AppendLine();
        builder.AppendLine($"Repository question: {question}");
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

        builder.AppendLine();
        builder.AppendLine("## Decision Outcome");
        builder.AppendLine();
        builder.AppendLine("TBD");
        builder.AppendLine();
        builder.AppendLine("### Consequences");
        builder.AppendLine();
        builder.AppendLine("- Good, because …");
        builder.AppendLine("- Bad, because …");
        return builder.ToString().TrimEnd();
    }

    private static string BuildConstraintSummary(IReadOnlyList<AdrPortal.Core.Entities.Adr> activeConstraints)
    {
        if (activeConstraints.Count is 0)
        {
            return string.Empty;
        }

        var references = activeConstraints
            .Take(4)
            .Select(adr => $"ADR-{adr.Number:0000} ({adr.Title})")
            .ToArray();
        return $"Active ADR constraints to respect: {string.Join(", ", references)}.";
    }

    private static OptionScoreResult ScoreOption(
        string option,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        AdrDraftForAnalysis draftAdr)
    {
        var optionTerms = Tokenize(option);
        var matchingDrivers = draftAdr.DecisionDrivers
            .Where(driver => HasTokenOverlap(optionTerms, Tokenize(driver)))
            .Select(driver => ShortenForSentence(driver, driver))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var contextTerms = Tokenize($"{draftAdr.ProblemStatement} {draftAdr.BodyMarkdown}");
        var contextSignalHits = optionTerms.Count(contextTerms.Contains);
        var supportingAdrNumbers = existingAdrs
            .Where(adr => HasTokenOverlap(optionTerms, Tokenize($"{adr.Title} {adr.Slug}")))
            .OrderBy(adr => adr.Number)
            .Take(4)
            .Select(adr => adr.Number)
            .ToArray();
        var normalized = 0.38
            + Math.Min(0.30, matchingDrivers.Length * 0.12)
            + Math.Min(0.18, contextSignalHits * 0.03)
            + Math.Min(0.14, supportingAdrNumbers.Length * 0.05);
        var bounded = Math.Clamp(normalized, 0.05, 0.99);
        return new OptionScoreResult
        {
            Option = option,
            Score = Math.Round(bounded, 2, MidpointRounding.AwayFromZero),
            MatchingDrivers = matchingDrivers,
            ContextSignalHits = contextSignalHits,
            SupportingAdrNumbers = supportingAdrNumbers
        };
    }

    private static string BuildOptionSummary(
        OptionScoreResult optionScore,
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs)
    {
        var pros = BuildOptionPros(optionScore, draftAdr, existingAdrs);
        var cons = BuildOptionCons(optionScore, draftAdr, existingAdrs);
        var leadPro = RemoveSentenceTerminal(pros[0]);
        var leadCon = RemoveSentenceTerminal(cons[0]);
        return $"Favors {leadPro} but introduces {leadCon}.";
    }

    private static string BuildOptionRationale(
        OptionScoreResult optionScore,
        AdrDraftForAnalysis draftAdr)
    {
        var rationaleParts = new List<string>
        {
            $"Score {optionScore.Score:0.00} is based on {optionScore.MatchingDrivers.Count} driver match(es) and {optionScore.ContextSignalHits} contextual signal(s)."
        };

        if (optionScore.SupportingAdrNumbers.Count > 0)
        {
            rationaleParts.Add($"Related ADR precedent: {FormatAdrNumbers(optionScore.SupportingAdrNumbers)}.");
        }
        else
        {
            rationaleParts.Add("No close ADR precedent was detected in titles/slugs.");
        }

        if (draftAdr.DecisionDrivers.Count > 0 && optionScore.MatchingDrivers.Count > 0)
        {
            rationaleParts.Add($"Aligned drivers: {string.Join(", ", optionScore.MatchingDrivers)}.");
        }

        return string.Join(' ', rationaleParts);
    }

    private static IReadOnlyList<string> BuildOptionPros(
        OptionScoreResult optionScore,
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs)
    {
        var pros = new List<string>();
        if (optionScore.MatchingDrivers.Count > 0)
        {
            pros.Add($"Directly supports driver(s): {string.Join(", ", optionScore.MatchingDrivers)}.");
        }
        else
        {
            pros.Add("Provides a concrete path for the current decision scope.");
        }

        if (optionScore.SupportingAdrNumbers.Count > 0)
        {
            pros.Add($"Builds on precedent from {FormatAdrNumbers(optionScore.SupportingAdrNumbers)}.");
        }
        else if (existingAdrs.Count is 0)
        {
            pros.Add("Can proceed independently even without historical ADR precedent.");
        }

        if (ContainsAny(optionScore.Option, "standardize", "single", "baseline", "platform"))
        {
            pros.Add("Improves consistency across teams and repositories.");
        }
        else if (ContainsAny(optionScore.Option, "phased", "pilot", "checkpoint", "rollout"))
        {
            pros.Add("Supports incremental rollout with measurable checkpoints.");
        }
        else if (ContainsAny(optionScore.Option, "keep current", "targeted improvements", "continue"))
        {
            pros.Add("Limits immediate disruption by reducing scope of change.");
        }

        if (pros.Count is 1 && draftAdr.DecisionDrivers.Count > 1)
        {
            pros.Add("Keeps multiple decision drivers visible during implementation planning.");
        }

        return pros
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildOptionCons(
        OptionScoreResult optionScore,
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs)
    {
        var cons = new List<string>();
        if (ContainsAny(optionScore.Option, "standardize", "single", "baseline", "platform"))
        {
            cons.Add("May reduce flexibility for edge-case workloads.");
        }
        else if (ContainsAny(optionScore.Option, "phased", "pilot", "checkpoint", "rollout"))
        {
            cons.Add("Delays full realization of benefits until later rollout phases.");
        }
        else if (ContainsAny(optionScore.Option, "keep current", "targeted improvements", "continue"))
        {
            cons.Add("Can preserve existing inconsistencies or technical debt.");
        }

        var minimumDriverCoverage = Math.Max(1, Math.Min(2, draftAdr.DecisionDrivers.Count));
        if (optionScore.MatchingDrivers.Count < minimumDriverCoverage)
        {
            cons.Add("Does not clearly satisfy all stated decision drivers without additional controls.");
        }

        if (existingAdrs.Count > 0 && optionScore.SupportingAdrNumbers.Count is 0)
        {
            cons.Add("Has limited precedent in the current ADR corpus, increasing validation effort.");
        }

        if (cons.Count is 0)
        {
            cons.Add("Introduces trade-offs that require explicit rollout and ownership planning.");
        }

        if (cons.Count is 1)
        {
            cons.Add("Needs measurable acceptance criteria before implementation commitment.");
        }

        return cons
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildOptionTradeOffs(
        OptionScoreResult optionScore,
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs)
    {
        var pros = BuildOptionPros(optionScore, draftAdr, existingAdrs);
        var cons = BuildOptionCons(optionScore, draftAdr, existingAdrs);
        return
        [
            RemoveSentenceTerminal(pros[0]),
            RemoveSentenceTerminal(cons[0])
        ];
    }

    private static IReadOnlyList<string> BuildRecommendationRisks(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        OptionScoreResult preferredOption,
        int optionCount)
    {
        var risks = new List<string>();
        risks.Add("Deterministic fallback cannot evaluate unstated organizational constraints or runtime evidence.");

        if (optionCount < MinimumRecommendationOptions)
        {
            risks.Add($"Only {optionCount} option(s) were available for ranking; add additional options manually for broader evaluation.");
        }

        if (draftAdr.DecisionDrivers.Count is 0)
        {
            risks.Add("Decision Drivers section is empty, reducing recommendation confidence.");
        }
        else
        {
            var minimumDriverCoverage = Math.Max(1, Math.Min(2, draftAdr.DecisionDrivers.Count));
            if (preferredOption.MatchingDrivers.Count < minimumDriverCoverage)
            {
                risks.Add("Recommended option does not map clearly to all key decision drivers; validate with stakeholders.");
            }
        }

        if (existingAdrs.Count > 0 && preferredOption.SupportingAdrNumbers.Count is 0)
        {
            risks.Add("No close ADR precedent was found for the recommended option, increasing implementation uncertainty.");
        }

        if (existingAdrs.Count is 0 && draftAdr.ConsideredOptions.Count < 2)
        {
            risks.Add("No existing ADR corpus is available and the draft has limited options; manual review should widen the decision space.");
        }

        return risks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildSuggestedAlternatives(
        string preferredOption,
        IReadOnlyList<OptionScoreResult> rankedOptions)
    {
        var alternatives = rankedOptions
            .Where(option => !string.Equals(option.Option, preferredOption, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .Select(option => $"{option.Option} (score {option.Score:0.00})")
            .ToList();
        if (alternatives.Count is 0)
        {
            alternatives.Add("Delay decision until additional constraints are documented and compared.");
        }

        return alternatives;
    }

    private static string BuildDecisionFit(
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        AdrDraftForAnalysis draftAdr,
        OptionScoreResult preferredOption)
    {
        if (existingAdrs.Count is 0)
        {
            return "No existing ADRs were available, so recommendation fit is based on draft content only.";
        }

        var acceptedCount = existingAdrs.Count(adr => adr.Status is AdrPortal.Core.Entities.AdrStatus.Accepted);
        var precedentText = preferredOption.SupportingAdrNumbers.Count is 0
            ? "No directly comparable ADR precedent was found."
            : $"Comparable ADRs: {FormatAdrNumbers(preferredOption.SupportingAdrNumbers)}.";
        var driverCoverageText = draftAdr.DecisionDrivers.Count is 0
            ? "Driver coverage is inferred from context text."
            : $"Recommendation aligns with {preferredOption.MatchingDrivers.Count} of {draftAdr.DecisionDrivers.Count} stated driver(s).";
        return $"Grounded against {existingAdrs.Count} ADR(s), including {acceptedCount} accepted baseline decision(s). {precedentText} {driverCoverageText}";
    }

    private static HashSet<string> BuildSignalTerms(AdrDraftForAnalysis draftAdr)
    {
        var values = new[]
        {
            draftAdr.Title,
            draftAdr.Slug,
            draftAdr.ProblemStatement,
            draftAdr.BodyMarkdown,
            string.Join(' ', draftAdr.DecisionDrivers),
            string.Join(' ', draftAdr.ConsideredOptions)
        };

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            foreach (var token in Tokenize(value ?? string.Empty))
            {
                terms.Add(token);
            }
        }

        return terms;
    }

    private static string BuildRecommendationSummary(
        OptionScoreResult preferredOption,
        OptionScoreResult? runnerUpOption,
        AdrDraftForAnalysis draftAdr)
    {
        var leadReason = preferredOption.MatchingDrivers.Count > 0
            ? $"it best aligns with driver(s): {string.Join(", ", preferredOption.MatchingDrivers)}"
            : "it has the strongest overall deterministic fit for the documented problem";
        if (runnerUpOption is null)
        {
            return $"Recommend '{preferredOption.Option}' because {leadReason}.";
        }

        var margin = Math.Max(0, preferredOption.Score - runnerUpOption.Score);
        var driverContext = draftAdr.DecisionDrivers.Count is 0
            ? "captured problem context"
            : $"{draftAdr.DecisionDrivers.Count} stated decision driver(s)";
        return $"Recommend '{preferredOption.Option}' because {leadReason}. It scores {preferredOption.Score:0.00} versus {runnerUpOption.Score:0.00} for '{runnerUpOption.Option}', giving a {margin:0.00} fit advantage against {driverContext}.";
    }

    private static string FormatAdrNumbers(IReadOnlyList<int> adrNumbers)
    {
        return string.Join(", ", adrNumbers.Select(number => $"ADR-{number:0000}"));
    }

    private static string ResolveDecisionScope(AdrDraftForAnalysis draftAdr)
    {
        if (!string.IsNullOrWhiteSpace(draftAdr.Title))
        {
            return $"the '{ShortenForSentence(draftAdr.Title, draftAdr.Title)}' decision";
        }

        if (!string.IsNullOrWhiteSpace(draftAdr.Slug))
        {
            return $"the '{draftAdr.Slug.Replace('-', ' ')}' decision";
        }

        return "the architecture decision";
    }

    private static void AddOptionCandidate(ICollection<string> options, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = WhitespaceRegex.Replace(value.Trim(), " ");
        if (normalized.Length < 8)
        {
            return;
        }

        if (options.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        options.Add(normalized);
    }

    private static string ShortenForSentence(string? value, string fallbackValue)
    {
        var normalized = WhitespaceRegex.Replace(value ?? string.Empty, " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallbackValue;
        }

        return normalized.Length <= 72
            ? normalized
            : $"{normalized[..72].TrimEnd()}…";
    }

    private static string RemoveSentenceTerminal(string value)
    {
        return value.Trim().TrimEnd('.', ';', ':');
    }

    private static HashSet<string> Tokenize(string value)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = NormalizeContent(value);
        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 4 || IgnoredSignalTerms.Contains(token))
            {
                continue;
            }

            terms.Add(token);
        }

        return terms;
    }

    private static bool HasTokenOverlap(ISet<string> left, ISet<string> right)
    {
        return left.Overlaps(right);
    }

    private static AffectedAdrResultItem? BuildAffectedItem(
        AdrPortal.Core.Entities.Adr existingAdr,
        ISet<string> draftTerms)
    {
        var existingText = NormalizeContent($"{existingAdr.Title} {existingAdr.Slug} {existingAdr.RawMarkdown}");
        var tokens = existingText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matches = tokens
            .Where(token => draftTerms.Contains(token))
            .Take(5)
            .ToArray();

        if (matches.Length is 0)
        {
            return null;
        }

        var level = matches.Length >= 4
            ? AdrImpactLevel.High
            : matches.Length >= 2
                ? AdrImpactLevel.Medium
                : AdrImpactLevel.Low;

        return new AffectedAdrResultItem
        {
            AdrNumber = existingAdr.Number,
            Title = existingAdr.Title,
            ImpactLevel = level,
            Rationale = $"Detected {matches.Length} shared signal(s): {string.Join(", ", matches)}.",
            Signals = matches
        };
    }

    private static IReadOnlyList<AdrDraftProposal> BuildProposals(
        IReadOnlyDictionary<string, FileSignal> fileSignals,
        IReadOnlyList<CodebaseScanChunk> chunks)
    {
        var proposals = new List<AdrDraftProposal>();
        var allSignals = fileSignals.Values.ToArray();
        if (allSignals.Length is 0)
        {
            return [];
        }

        AddIfAny(
            proposals,
            allSignals.Where(signal => signal.ContainsAspire).ToArray(),
            "Use .NET Aspire for local orchestration",
            "use-dotnet-aspire-local-orchestration",
            "The codebase includes multiple application resources and environment wiring that need a standard local orchestration model.",
            ["Maintain consistent local startup and service wiring", "Reduce environment drift between services"],
            ["Use .NET Aspire AppHost as the canonical local orchestrator", "Use ad-hoc script-based startup commands per service"],
            chunks,
            confidence: 0.93);

        AddIfAny(
            proposals,
            allSignals.Where(signal => signal.ContainsEfCore).ToArray(),
            "Persist portal configuration with EF Core and SQLite",
            "persist-portal-configuration-efcore-sqlite",
            "The codebase uses Entity Framework Core with SQLite migrations for repository and library metadata.",
            ["Need reliable local persistence without external infrastructure", "Support deterministic schema migrations"],
            ["Use EF Core with SQLite as persistence baseline", "Replace with custom file-based persistence"],
            chunks,
            confidence: 0.89);

        AddIfAny(
            proposals,
            allSignals.Where(signal => signal.ContainsTesting).ToArray(),
            "Standardize tests on TUnit",
            "standardize-tests-on-tunit",
            "The repository contains TUnit-based projects and dotnet test workflows requiring a single testing framework.",
            ["Keep testing style consistent across layers", "Maintain fast deterministic test execution"],
            ["Use TUnit across Core, Infrastructure, and Web tests", "Adopt mixed testing frameworks per project"],
            chunks,
            confidence: 0.84);

        AddIfAny(
            proposals,
            allSignals.Where(signal => signal.ContainsSecurity).ToArray(),
            "Enforce secure defaults in web and ADR workflows",
            "enforce-secure-defaults-in-web-and-adr-workflows",
            "The codebase contains authentication, authorization, HTTPS, and input validation concerns that should be captured in an ADR.",
            ["Protect repository operations and UI workflows by default", "Prevent unsafe markdown or HTML content handling"],
            ["Adopt strict secure defaults and explicit validation", "Allow permissive input handling with post-processing"],
            chunks,
            confidence: 0.8);

        AddIfAny(
            proposals,
            allSignals.Where(signal => signal.ContainsObservability).ToArray(),
            "Adopt OpenTelemetry and service defaults for observability",
            "adopt-opentelemetry-and-service-defaults",
            "The repository references service defaults and telemetry packages that indicate a cross-cutting observability direction.",
            ["Need centralized traces/logs for distributed workflows", "Ensure diagnostics are available for local and production runs"],
            ["Use OpenTelemetry with shared service defaults", "Collect logs only without distributed tracing"],
            chunks,
            confidence: 0.78);

        return proposals
            .OrderByDescending(proposal => proposal.ConfidenceScore)
            .ThenBy(proposal => proposal.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddIfAny(
        ICollection<AdrDraftProposal> proposals,
        IReadOnlyCollection<FileSignal> matchingSignals,
        string title,
        string suggestedSlug,
        string problemStatement,
        IReadOnlyList<string> decisionDrivers,
        IReadOnlyList<string> initialOptions,
        IReadOnlyList<CodebaseScanChunk> chunks,
        double confidence)
    {
        if (matchingSignals.Count is 0)
        {
            return;
        }

        var evidenceFiles = matchingSignals
            .Select(signal => signal.RelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        var evidenceChunkIds = chunks
            .Where(chunk => evidenceFiles.Contains(chunk.RepoRelativePath, StringComparer.OrdinalIgnoreCase))
            .Select(chunk => chunk.ChunkId)
            .Take(8)
            .ToArray();

        var normalizedSlug = NormalizeSlug(suggestedSlug);
        proposals.Add(
            new AdrDraftProposal
            {
                ProposalId = BuildProposalId(title, evidenceFiles),
                Title = title,
                SuggestedSlug = normalizedSlug,
                ProblemStatement = problemStatement,
                DecisionDrivers = decisionDrivers,
                InitialOptions = initialOptions,
                EvidenceFiles = evidenceFiles,
                EvidenceChunkIds = evidenceChunkIds,
                ConfidenceScore = confidence
            });
    }

    private static string NormalizeSlug(string slug)
    {
        var lowered = slug.Trim().ToLowerInvariant();
        var collapsed = SlugRegex.Replace(lowered, "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "adr-bootstrap-proposal" : collapsed;
    }

    private static string BuildProposalId(string title, IReadOnlyCollection<string> evidenceFiles)
    {
        var payload = $"{title}::{string.Join('|', evidenceFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private sealed class FileSignal
    {
        public required string RelativePath { get; init; }

        public bool ContainsAspire { get; init; }

        public bool ContainsEfCore { get; init; }

        public bool ContainsTesting { get; init; }

        public bool ContainsSecurity { get; init; }

        public bool ContainsObservability { get; init; }
    }
}
