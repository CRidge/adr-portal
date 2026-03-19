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
