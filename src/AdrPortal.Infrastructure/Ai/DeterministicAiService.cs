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
    private const int MaximumFiles = 160;
    private const int MaximumCharactersPerChunk = 1200;
    private const int MaximumPreviewCharacters = 360;

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

        var normalizedOptions = draftAdr.ConsideredOptions
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedOptions.Length is 0)
        {
            normalizedOptions =
            [
                "Retain current architecture with incremental improvements",
                "Introduce focused platform standardization"
            ];
        }

        var rankedOptions = normalizedOptions
            .Select(option => new
            {
                Option = option,
                Score = ScoreOption(option, existingAdrs, draftAdr)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Option, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var preferred = rankedOptions[0];
        var optionRecommendations = rankedOptions
            .Select(
                item => new AdrOptionRecommendation
                {
                    OptionName = item.Option,
                    Summary = BuildOptionSummary(item.Option),
                    Score = item.Score,
                    Rationale = BuildOptionRationale(item.Option, item.Score, existingAdrs, draftAdr),
                    TradeOffs = BuildOptionTradeOffs(item.Option)
                })
            .ToArray();

        var risks = BuildRecommendationRisks(draftAdr, existingAdrs);
        var alternatives = BuildSuggestedAlternatives(preferred.Option, normalizedOptions);
        var groundingNumbers = existingAdrs
            .OrderBy(adr => adr.Number)
            .Take(8)
            .Select(adr => adr.Number)
            .ToArray();

        var recommendation = new AdrEvaluationRecommendation
        {
            PreferredOption = preferred.Option,
            RecommendationSummary = $"Prefer '{preferred.Option}' based on deterministic alignment with existing ADR constraints.",
            DecisionFit = BuildDecisionFit(existingAdrs, draftAdr),
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

    private static double ScoreOption(
        string option,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        AdrDraftForAnalysis draftAdr)
    {
        var titleHits = existingAdrs.Count(adr => adr.Title.Contains(option, StringComparison.OrdinalIgnoreCase));
        var driverHits = draftAdr.DecisionDrivers.Count(driver =>
            option.Contains(driver, StringComparison.OrdinalIgnoreCase)
            || driver.Contains(option, StringComparison.OrdinalIgnoreCase));
        var normalized = 0.45 + Math.Min(0.25, titleHits * 0.08) + Math.Min(0.2, driverHits * 0.05);
        var bounded = Math.Clamp(normalized, 0.05, 0.99);
        return Math.Round(bounded, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildOptionSummary(string option)
    {
        return $"Option '{option}' emphasizes maintainable ADR consistency and controlled implementation risk.";
    }

    private static string BuildOptionRationale(
        string option,
        double score,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        AdrDraftForAnalysis draftAdr)
    {
        var comparableAdrs = existingAdrs
            .Where(adr =>
                adr.Title.Contains(option, StringComparison.OrdinalIgnoreCase)
                || option.Contains(adr.Slug, StringComparison.OrdinalIgnoreCase))
            .OrderBy(adr => adr.Number)
            .Take(3)
            .Select(adr => $"ADR-{adr.Number:0000}")
            .ToArray();

        var grounding = comparableAdrs.Length is 0
            ? "No close title match in existing ADRs."
            : $"Grounded by {string.Join(", ", comparableAdrs)}.";

        var driverCount = draftAdr.DecisionDrivers.Count;
        return $"{grounding} Deterministic score {score:0.00} reflects alignment with {driverCount} decision driver(s).";
    }

    private static IReadOnlyList<string> BuildOptionTradeOffs(string option)
    {
        return
        [
            $"Requires explicit implementation scope for '{option}'.",
            "May increase coordination overhead during rollout."
        ];
    }

    private static IReadOnlyList<string> BuildRecommendationRisks(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs)
    {
        var risks = new List<string>
        {
            "Deterministic fallback cannot evaluate nuanced architectural semantics from external model context."
        };

        if (draftAdr.ConsideredOptions.Count < 2)
        {
            risks.Add("Draft ADR has fewer than two considered options, reducing recommendation confidence.");
        }

        if (existingAdrs.Count is 0)
        {
            risks.Add("No existing ADR corpus available for contextual grounding.");
        }

        return risks;
    }

    private static IReadOnlyList<string> BuildSuggestedAlternatives(string preferredOption, IReadOnlyList<string> options)
    {
        var alternatives = options
            .Where(option => !string.Equals(option, preferredOption, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
        if (alternatives.Count is 0)
        {
            alternatives.Add("Delay decision until additional empirical constraints are documented.");
        }

        return alternatives;
    }

    private static string BuildDecisionFit(
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        AdrDraftForAnalysis draftAdr)
    {
        if (existingAdrs.Count is 0)
        {
            return "No existing ADRs were available, so recommendation fit is based on draft content only.";
        }

        var acceptedCount = existingAdrs.Count(adr => adr.Status is AdrPortal.Core.Entities.AdrStatus.Accepted);
        return $"Existing corpus includes {existingAdrs.Count} ADR(s), with {acceptedCount} accepted baseline decision(s), informing recommendation fit.";
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
            var normalized = NormalizeContent(value ?? string.Empty);
            foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Length < 4)
                {
                    continue;
                }

                terms.Add(token);
            }
        }

        return terms;
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
