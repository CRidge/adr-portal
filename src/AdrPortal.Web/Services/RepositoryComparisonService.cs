using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Compares ADR catalogs between repositories and imports selected source ADRs into a target repository.
/// </summary>
public sealed class RepositoryComparisonService(
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IAiService aiService,
    IGlobalAdrStore globalAdrStore)
{
    private static readonly Regex SlugSanitizerRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string ComparisonPromptHeading = "## Source ADR comparison draft";

    /// <summary>
    /// Compares source and target repositories, loads ADRs from both, and returns AI-ranked source ADR rows.
    /// </summary>
    /// <param name="sourceRepositoryId">Source repository identifier.</param>
    /// <param name="targetRepositoryId">Target repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Ranked comparison result.</returns>
    public async Task<RepositoryComparisonResult> CompareAsync(
        int sourceRepositoryId,
        int targetRepositoryId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sourceRepository = await ResolveRepositoryAsync(sourceRepositoryId, ct);
        var targetRepository = await ResolveRepositoryAsync(targetRepositoryId, ct);
        EnsureDistinctRepositories(sourceRepository, targetRepository);

        var sourceAdrRepository = await madrRepositoryFactory.CreateAsync(sourceRepository, ct);
        var targetAdrRepository = await madrRepositoryFactory.CreateAsync(targetRepository, ct);
        var sourceAdrs = await sourceAdrRepository.GetAllAsync(ct);
        var targetAdrs = await targetAdrRepository.GetAllAsync(ct);

        var rankedRows = new List<RepositoryComparisonRankedAdr>(sourceAdrs.Count);
        foreach (var sourceAdr in sourceAdrs.OrderBy(adr => adr.Number))
        {
            ct.ThrowIfCancellationRequested();

            var analysisDraft = BuildComparisonDraft(sourceAdr);
            var affected = await aiService.FindAffectedAdrsAsync(analysisDraft, targetAdrs, ct);
            var maxImpact = GetHighestImpactLevel(affected.Items);
            var normalizedScore = NormalizeScore(maxImpact, affected.Items.Count);
            var summary = BuildSummary(sourceAdr, affected);
            var matches = affected.Items
                .OrderByDescending(item => item.ImpactLevel)
                .ThenBy(item => item.AdrNumber)
                .Select(item => MapTargetMatch(item, targetAdrs))
                .ToArray();

            rankedRows.Add(
                new RepositoryComparisonRankedAdr
                {
                    SourceAdrNumber = sourceAdr.Number,
                    SourceTitle = sourceAdr.Title,
                    SourceSlug = sourceAdr.Slug,
                    SourceStatus = sourceAdr.Status,
                    RelevanceScore = normalizedScore,
                    RelevanceSummary = summary,
                    TargetMatches = matches,
                    IsFallback = affected.IsFallback,
                    FallbackReason = affected.FallbackReason,
                    IsLibraryLinked = sourceAdr.GlobalId is not null && sourceAdr.GlobalVersion is not null,
                    GlobalId = sourceAdr.GlobalId,
                    GlobalVersion = sourceAdr.GlobalVersion
                });
        }

        var orderedRows = rankedRows
            .OrderByDescending(row => row.RelevanceScore)
            .ThenByDescending(row => row.TargetMatches.Count)
            .ThenBy(row => row.SourceAdrNumber)
            .ToArray();

        return new RepositoryComparisonResult
        {
            SourceRepository = sourceRepository,
            TargetRepository = targetRepository,
            SourceAdrCount = sourceAdrs.Count,
            TargetAdrCount = targetAdrs.Count,
            RankedSourceAdrs = orderedRows,
            ComparedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Imports selected source ADRs into the target repository as proposed and maintains global mapping state.
    /// </summary>
    /// <param name="sourceRepositoryId">Source repository identifier.</param>
    /// <param name="targetRepositoryId">Target repository identifier.</param>
    /// <param name="selectedSourceAdrNumbers">Selected source ADR numbers.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Import workflow result.</returns>
    public async Task<RepositoryComparisonImportResult> ImportSelectedAsync(
        int sourceRepositoryId,
        int targetRepositoryId,
        IReadOnlyCollection<int> selectedSourceAdrNumbers,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(selectedSourceAdrNumbers);
        ct.ThrowIfCancellationRequested();

        var sourceRepository = await ResolveRepositoryAsync(sourceRepositoryId, ct);
        var targetRepository = await ResolveRepositoryAsync(targetRepositoryId, ct);
        EnsureDistinctRepositories(sourceRepository, targetRepository);

        var sourceAdrRepository = await madrRepositoryFactory.CreateAsync(sourceRepository, ct);
        var targetAdrRepository = await madrRepositoryFactory.CreateAsync(targetRepository, ct);
        var sourceAdrs = await sourceAdrRepository.GetAllAsync(ct);
        var sourceByNumber = sourceAdrs.ToDictionary(adr => adr.Number);

        var selectedNumbers = selectedSourceAdrNumbers
            .Distinct()
            .OrderBy(number => number)
            .ToArray();
        if (selectedNumbers.Length is 0)
        {
            throw new InvalidOperationException("Select at least one source ADR to import.");
        }

        var unresolved = selectedNumbers
            .Where(number => !sourceByNumber.ContainsKey(number))
            .ToArray();
        if (unresolved.Length > 0)
        {
            var unresolvedText = string.Join(", ", unresolved);
            throw new InvalidOperationException($"Selected source ADRs were not found: {unresolvedText}.");
        }

        var existingTargetAdrs = await targetAdrRepository.GetAllAsync(ct);
        var usedSlugs = existingTargetAdrs
            .Select(adr => adr.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextNumber = await targetAdrRepository.GetNextNumberAsync(ct);
        var normalizedTargetAdrFolder = RepositoryPathSecurity.NormalizeRelativePath(targetRepository.AdrFolder);

        var importedItems = new List<RepositoryComparisonImportItem>(selectedNumbers.Length);
        var globalRegistrationsCreated = 0;
        var globalInstancesUpserted = 0;
        var utcNow = DateTime.UtcNow;

        foreach (var sourceNumber in selectedNumbers)
        {
            ct.ThrowIfCancellationRequested();
            var sourceAdr = sourceByNumber[sourceNumber];
            var sourceGlobalVersion = sourceAdr.GlobalVersion;
            var sourceGlobalId = sourceAdr.GlobalId;

            var resolvedGlobalId = sourceGlobalId;
            var resolvedGlobalVersion = sourceGlobalVersion;
            var updateAvailableForImportedInstance = false;

            if (sourceGlobalId is not null && sourceGlobalVersion is not null && sourceGlobalVersion.Value >= 1)
            {
                var existingGlobal = await globalAdrStore.GetByIdAsync(sourceGlobalId.Value, ct);
                if (existingGlobal is null)
                {
                    var registeredAtUtc = utcNow;
                    _ = await globalAdrStore.AddAsync(
                        new GlobalAdr
                        {
                            GlobalId = sourceGlobalId.Value,
                            Title = sourceAdr.Title,
                            CurrentVersion = sourceGlobalVersion.Value,
                            RegisteredAtUtc = registeredAtUtc,
                            LastUpdatedAtUtc = registeredAtUtc,
                            Instances = [],
                            Versions = [],
                            UpdateProposals = []
                        },
                        ct);
                    globalRegistrationsCreated++;

                    _ = await globalAdrStore.AddVersionAsync(
                        new GlobalAdrVersion
                        {
                            GlobalId = sourceGlobalId.Value,
                            VersionNumber = sourceGlobalVersion.Value,
                            Title = sourceAdr.Title,
                            MarkdownContent = sourceAdr.RawMarkdown,
                            CreatedAtUtc = registeredAtUtc
                        },
                        ct);
                }
                else
                {
                    updateAvailableForImportedInstance = existingGlobal.CurrentVersion > sourceGlobalVersion.Value;
                }
            }
            else
            {
                resolvedGlobalId = null;
                resolvedGlobalVersion = null;
            }

            var targetNumber = nextNumber++;
            var uniqueSlug = CreateUniqueSlug(sourceAdr.Slug, usedSlugs);
            var targetRelativePath = $"{normalizedTargetAdrFolder}/adr-{targetNumber:0000}-{uniqueSlug}.md";
            var persistedStatus = AdrStatus.Proposed;
            var importedAdr = new Adr
            {
                Number = targetNumber,
                Slug = uniqueSlug,
                RepoRelativePath = targetRelativePath,
                Title = sourceAdr.Title,
                Status = persistedStatus,
                Date = sourceAdr.Date,
                GlobalId = resolvedGlobalId,
                GlobalVersion = resolvedGlobalVersion,
                DecisionMakers = sourceAdr.DecisionMakers,
                Consulted = sourceAdr.Consulted,
                Informed = sourceAdr.Informed,
                SupersededByNumber = null,
                RawMarkdown = sourceAdr.RawMarkdown
            };

            var persistedAdr = await targetAdrRepository.WriteAsync(importedAdr, ct);
            importedItems.Add(
                new RepositoryComparisonImportItem
                {
                    SourceAdrNumber = sourceAdr.Number,
                    ImportedTargetAdrNumber = persistedAdr.Number,
                    ImportedTitle = persistedAdr.Title,
                    ImportedSlug = persistedAdr.Slug,
                    ImportedStatus = persistedAdr.Status,
                    GlobalId = persistedAdr.GlobalId,
                    GlobalVersion = persistedAdr.GlobalVersion
                });

            if (persistedAdr.GlobalId is not null && persistedAdr.GlobalVersion is not null)
            {
                _ = await globalAdrStore.UpsertInstanceAsync(
                    new GlobalAdrInstance
                    {
                        GlobalId = persistedAdr.GlobalId.Value,
                        RepositoryId = targetRepository.Id,
                        LocalAdrNumber = persistedAdr.Number,
                        RepoRelativePath = persistedAdr.RepoRelativePath,
                        LastKnownStatus = persistedAdr.Status,
                        BaseTemplateVersion = persistedAdr.GlobalVersion.Value,
                        HasLocalChanges = false,
                        UpdateAvailable = updateAvailableForImportedInstance,
                        LastReviewedAtUtc = utcNow
                    },
                    ct);
                globalInstancesUpserted++;
            }
        }

        return new RepositoryComparisonImportResult
        {
            SourceRepository = sourceRepository,
            TargetRepository = targetRepository,
            ImportedItems = importedItems,
            GlobalRegistrationsCreated = globalRegistrationsCreated,
            GlobalInstancesUpserted = globalInstancesUpserted
        };
    }

    /// <summary>
    /// Resolves one managed repository by identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Resolved repository.</returns>
    /// <exception cref="InvalidOperationException">Thrown when repository cannot be found.</exception>
    private async Task<ManagedRepository> ResolveRepositoryAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            throw new InvalidOperationException($"Repository '{repositoryId}' was not found.");
        }

        return repository;
    }

    /// <summary>
    /// Ensures source and target repositories are distinct.
    /// </summary>
    /// <param name="sourceRepository">Source repository.</param>
    /// <param name="targetRepository">Target repository.</param>
    /// <exception cref="InvalidOperationException">Thrown when both repositories are equal.</exception>
    private static void EnsureDistinctRepositories(ManagedRepository sourceRepository, ManagedRepository targetRepository)
    {
        if (sourceRepository.Id == targetRepository.Id)
        {
            throw new InvalidOperationException("Source and target repositories must be different.");
        }
    }

    /// <summary>
    /// Builds a draft payload for AI relevance analysis from source ADR content.
    /// </summary>
    /// <param name="sourceAdr">Source ADR.</param>
    /// <returns>Draft payload.</returns>
    private static AdrDraftForAnalysis BuildComparisonDraft(Adr sourceAdr)
    {
        var normalizedMarkdown = NormalizeMarkdown(sourceAdr.RawMarkdown);
        return new AdrDraftForAnalysis
        {
            Title = sourceAdr.Title,
            Slug = sourceAdr.Slug,
            ProblemStatement = $"{ComparisonPromptHeading}: {sourceAdr.Title}",
            BodyMarkdown = normalizedMarkdown,
            DecisionDrivers = sourceAdr.DecisionMakers,
            ConsideredOptions = []
        };
    }

    /// <summary>
    /// Maps an affected target item to a compare target match view model.
    /// </summary>
    /// <param name="item">Affected item from AI analysis.</param>
    /// <param name="targetAdrs">Target repository ADR corpus.</param>
    /// <returns>Mapped target match.</returns>
    private static RepositoryComparisonTargetMatch MapTargetMatch(
        AffectedAdrResultItem item,
        IReadOnlyList<Adr> targetAdrs)
    {
        var targetAdr = targetAdrs.FirstOrDefault(adr => adr.Number == item.AdrNumber);
        if (targetAdr is null)
        {
            return new RepositoryComparisonTargetMatch
            {
                TargetAdrNumber = item.AdrNumber,
                TargetTitle = item.Title,
                TargetStatus = AdrStatus.Proposed,
                ImpactLevel = item.ImpactLevel,
                Rationale = item.Rationale,
                Signals = item.Signals
            };
        }

        return new RepositoryComparisonTargetMatch
        {
            TargetAdrNumber = targetAdr.Number,
            TargetTitle = targetAdr.Title,
            TargetStatus = targetAdr.Status,
            ImpactLevel = item.ImpactLevel,
            Rationale = item.Rationale,
            Signals = item.Signals
        };
    }

    /// <summary>
    /// Calculates normalized relevance score from AI impact data.
    /// </summary>
    /// <param name="highestImpact">Highest impact level in affected items.</param>
    /// <param name="matchCount">Count of affected items.</param>
    /// <returns>Normalized score in [0, 1].</returns>
    private static double NormalizeScore(AdrImpactLevel highestImpact, int matchCount)
    {
        if (matchCount <= 0)
        {
            return 0.1;
        }

        var baseScore = highestImpact switch
        {
            AdrImpactLevel.High => 0.85,
            AdrImpactLevel.Medium => 0.6,
            AdrImpactLevel.Low => 0.35,
            _ => 0.2
        };
        var spreadBonus = Math.Min(0.1, matchCount * 0.02);
        var score = Math.Clamp(baseScore + spreadBonus, 0.0, 1.0);
        return Math.Round(score, 3, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Extracts highest impact level from affected ADR items.
    /// </summary>
    /// <param name="items">Affected item collection.</param>
    /// <returns>Highest impact level, defaulting to low.</returns>
    private static AdrImpactLevel GetHighestImpactLevel(IReadOnlyList<AffectedAdrResultItem> items)
    {
        if (items.Count is 0)
        {
            return AdrImpactLevel.Low;
        }

        return items
            .Select(item => item.ImpactLevel)
            .Max();
    }

    /// <summary>
    /// Builds row summary text from AI result metadata.
    /// </summary>
    /// <param name="sourceAdr">Source ADR row.</param>
    /// <param name="analysis">Affected analysis result.</param>
    /// <returns>Summary text.</returns>
    private static string BuildSummary(Adr sourceAdr, AffectedAdrAnalysisResult analysis)
    {
        if (analysis.Items.Count is 0)
        {
            return $"No direct target ADR overlaps found for ADR-{sourceAdr.Number.ToString("0000", CultureInfo.InvariantCulture)}.";
        }

        return $"{analysis.Summary} ({analysis.Items.Count} target match(es)).";
    }

    /// <summary>
    /// Creates a unique slug value in target repository context.
    /// </summary>
    /// <param name="sourceSlug">Source slug seed.</param>
    /// <param name="usedSlugs">Set of already-used target slugs.</param>
    /// <returns>Unique slug.</returns>
    private static string CreateUniqueSlug(string sourceSlug, ISet<string> usedSlugs)
    {
        var normalized = NormalizeSlug(sourceSlug);
        if (usedSlugs.Add(normalized))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized}-{suffix}";
            if (usedSlugs.Add(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    /// <summary>
    /// Normalizes slug text to repository-safe lower-case kebab case.
    /// </summary>
    /// <param name="value">Input slug value.</param>
    /// <returns>Normalized slug.</returns>
    private static string NormalizeSlug(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var collapsed = SlugSanitizerRegex.Replace(lowered, "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "imported-adr" : collapsed;
    }

    /// <summary>
    /// Normalizes markdown line endings and trims whitespace.
    /// </summary>
    /// <param name="value">Markdown content.</param>
    /// <returns>Normalized markdown.</returns>
    private static string NormalizeMarkdown(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
