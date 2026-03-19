namespace AdrPortal.Core.Ai;

/// <summary>
/// Represents normalized ADR draft content used for AI analysis flows.
/// </summary>
public sealed record AdrDraftForAnalysis
{
    /// <summary>
    /// Gets the draft ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the draft ADR slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the draft problem statement extracted from markdown content.
    /// </summary>
    public required string ProblemStatement { get; init; }

    /// <summary>
    /// Gets the normalized markdown body used for lexical grounding.
    /// </summary>
    public required string BodyMarkdown { get; init; }

    /// <summary>
    /// Gets decision drivers extracted from the draft content.
    /// </summary>
    public IReadOnlyList<string> DecisionDrivers { get; init; } = [];

    /// <summary>
    /// Gets considered options extracted from the draft content.
    /// </summary>
    public IReadOnlyList<string> ConsideredOptions { get; init; } = [];
}

/// <summary>
/// Represents one scored option in an ADR recommendation result.
/// </summary>
public sealed record AdrOptionRecommendation
{
    /// <summary>
    /// Gets the option name.
    /// </summary>
    public required string OptionName { get; init; }

    /// <summary>
    /// Gets a concise summary for this option.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the deterministic score for this option.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Gets rationale text for this option.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets trade-offs associated with this option.
    /// </summary>
    public IReadOnlyList<string> TradeOffs { get; init; } = [];
}

/// <summary>
/// Represents structured recommendation output for draft ADR evaluation.
/// </summary>
public sealed record AdrEvaluationRecommendation
{
    /// <summary>
    /// Gets the preferred option selected by deterministic scoring.
    /// </summary>
    public required string PreferredOption { get; init; }

    /// <summary>
    /// Gets the summary recommendation statement.
    /// </summary>
    public required string RecommendationSummary { get; init; }

    /// <summary>
    /// Gets high-level fit description against existing ADRs.
    /// </summary>
    public required string DecisionFit { get; init; }

    /// <summary>
    /// Gets scored options and rationale details.
    /// </summary>
    public IReadOnlyList<AdrOptionRecommendation> Options { get; init; } = [];

    /// <summary>
    /// Gets identified risks related to the recommendation.
    /// </summary>
    public IReadOnlyList<string> Risks { get; init; } = [];

    /// <summary>
    /// Gets suggested alternatives to the preferred option.
    /// </summary>
    public IReadOnlyList<string> SuggestedAlternatives { get; init; } = [];

    /// <summary>
    /// Gets ADR numbers used for deterministic grounding context.
    /// </summary>
    public IReadOnlyList<int> GroundingAdrNumbers { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether deterministic fallback was used.
    /// </summary>
    public required bool IsFallback { get; init; }

    /// <summary>
    /// Gets the fallback reason when fallback execution is used.
    /// </summary>
    public string? FallbackReason { get; init; }
}

/// <summary>
/// Represents relative impact levels for affected ADR analysis.
/// </summary>
public enum AdrImpactLevel
{
    /// <summary>
    /// Low relative impact.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium relative impact.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High relative impact.
    /// </summary>
    High = 2
}

/// <summary>
/// Represents one affected ADR item and associated rationale.
/// </summary>
public sealed record AffectedAdrResultItem
{
    /// <summary>
    /// Gets the affected ADR number.
    /// </summary>
    public required int AdrNumber { get; init; }

    /// <summary>
    /// Gets the affected ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the impact level.
    /// </summary>
    public required AdrImpactLevel ImpactLevel { get; init; }

    /// <summary>
    /// Gets rationale describing why the ADR is impacted.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets lexical signals that contributed to this impact classification.
    /// </summary>
    public IReadOnlyList<string> Signals { get; init; } = [];
}

/// <summary>
/// Represents structured affected ADR analysis output.
/// </summary>
public sealed record AffectedAdrAnalysisResult
{
    /// <summary>
    /// Gets affected ADR items.
    /// </summary>
    public IReadOnlyList<AffectedAdrResultItem> Items { get; init; } = [];

    /// <summary>
    /// Gets summary text for the analysis outcome.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets a value indicating whether deterministic fallback was used.
    /// </summary>
    public required bool IsFallback { get; init; }

    /// <summary>
    /// Gets the fallback reason when fallback execution is used.
    /// </summary>
    public string? FallbackReason { get; init; }
}
