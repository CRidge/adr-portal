using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents one relevance-ranked source ADR row for source-to-target comparison.
/// </summary>
public sealed record RepositoryComparisonRankedAdr
{
    /// <summary>
    /// Gets the source ADR number.
    /// </summary>
    public required int SourceAdrNumber { get; init; }

    /// <summary>
    /// Gets the source ADR title.
    /// </summary>
    public required string SourceTitle { get; init; }

    /// <summary>
    /// Gets the source ADR slug.
    /// </summary>
    public required string SourceSlug { get; init; }

    /// <summary>
    /// Gets the source ADR status.
    /// </summary>
    public required AdrStatus SourceStatus { get; init; }

    /// <summary>
    /// Gets normalized relevance score in the range [0, 1].
    /// </summary>
    public required double RelevanceScore { get; init; }

    /// <summary>
    /// Gets summary rationale for the relevance score.
    /// </summary>
    public required string RelevanceSummary { get; init; }

    /// <summary>
    /// Gets target ADR matches returned by AI analysis.
    /// </summary>
    public IReadOnlyList<RepositoryComparisonTargetMatch> TargetMatches { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether deterministic fallback AI behavior was used.
    /// </summary>
    public required bool IsFallback { get; init; }

    /// <summary>
    /// Gets fallback reason text when deterministic fallback AI behavior was used.
    /// </summary>
    public string? FallbackReason { get; init; }

    /// <summary>
    /// Gets a value indicating whether this ADR is linked to a global ADR library entry.
    /// </summary>
    public required bool IsLibraryLinked { get; init; }

    /// <summary>
    /// Gets the linked global ADR identifier when available.
    /// </summary>
    public Guid? GlobalId { get; init; }

    /// <summary>
    /// Gets the linked global ADR version when available.
    /// </summary>
    public int? GlobalVersion { get; init; }
}

/// <summary>
/// Represents one target ADR match contributing to a source ADR relevance score.
/// </summary>
public sealed record RepositoryComparisonTargetMatch
{
    /// <summary>
    /// Gets the target ADR number.
    /// </summary>
    public required int TargetAdrNumber { get; init; }

    /// <summary>
    /// Gets the target ADR title.
    /// </summary>
    public required string TargetTitle { get; init; }

    /// <summary>
    /// Gets the target ADR status.
    /// </summary>
    public required AdrStatus TargetStatus { get; init; }

    /// <summary>
    /// Gets impact level assigned by AI analysis.
    /// </summary>
    public required AdrImpactLevel ImpactLevel { get; init; }

    /// <summary>
    /// Gets rationale text for this match.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets lexical signals contributing to this match.
    /// </summary>
    public IReadOnlyList<string> Signals { get; init; } = [];
}

/// <summary>
/// Represents source-to-target repository comparison output.
/// </summary>
public sealed record RepositoryComparisonResult
{
    /// <summary>
    /// Gets source repository metadata.
    /// </summary>
    public required ManagedRepository SourceRepository { get; init; }

    /// <summary>
    /// Gets target repository metadata.
    /// </summary>
    public required ManagedRepository TargetRepository { get; init; }

    /// <summary>
    /// Gets source ADR count.
    /// </summary>
    public required int SourceAdrCount { get; init; }

    /// <summary>
    /// Gets target ADR count.
    /// </summary>
    public required int TargetAdrCount { get; init; }

    /// <summary>
    /// Gets ranked relevance rows for source ADRs.
    /// </summary>
    public required IReadOnlyList<RepositoryComparisonRankedAdr> RankedSourceAdrs { get; init; }

    /// <summary>
    /// Gets UTC timestamp when comparison was generated.
    /// </summary>
    public required DateTime ComparedAtUtc { get; init; }
}

/// <summary>
/// Represents one source ADR import-to-target outcome.
/// </summary>
public sealed record RepositoryComparisonImportItem
{
    /// <summary>
    /// Gets source ADR number.
    /// </summary>
    public required int SourceAdrNumber { get; init; }

    /// <summary>
    /// Gets target ADR number assigned during import.
    /// </summary>
    public required int ImportedTargetAdrNumber { get; init; }

    /// <summary>
    /// Gets imported ADR title.
    /// </summary>
    public required string ImportedTitle { get; init; }

    /// <summary>
    /// Gets imported ADR slug.
    /// </summary>
    public required string ImportedSlug { get; init; }

    /// <summary>
    /// Gets imported ADR status.
    /// </summary>
    public required AdrStatus ImportedStatus { get; init; }

    /// <summary>
    /// Gets linked global ADR identifier when available.
    /// </summary>
    public Guid? GlobalId { get; init; }

    /// <summary>
    /// Gets linked global ADR version when available.
    /// </summary>
    public int? GlobalVersion { get; init; }
}

/// <summary>
/// Represents import workflow output for source-to-target comparison.
/// </summary>
public sealed record RepositoryComparisonImportResult
{
    /// <summary>
    /// Gets source repository metadata.
    /// </summary>
    public required ManagedRepository SourceRepository { get; init; }

    /// <summary>
    /// Gets target repository metadata.
    /// </summary>
    public required ManagedRepository TargetRepository { get; init; }

    /// <summary>
    /// Gets imported ADRs written to the target repository.
    /// </summary>
    public required IReadOnlyList<RepositoryComparisonImportItem> ImportedItems { get; init; }

    /// <summary>
    /// Gets count of global ADR registrations created during import.
    /// </summary>
    public required int GlobalRegistrationsCreated { get; init; }

    /// <summary>
    /// Gets count of global ADR instance mappings upserted during import.
    /// </summary>
    public required int GlobalInstancesUpserted { get; init; }
}
