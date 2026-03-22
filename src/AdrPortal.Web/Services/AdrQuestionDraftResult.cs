using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Defines context modes for question-driven ADR draft generation.
/// </summary>
public enum AdrQuestionContextMode
{
    /// <summary>
    /// Repository mode treats accepted ADRs as active constraints.
    /// </summary>
    Repository = 0,

    /// <summary>
    /// Global library mode treats existing ADRs as guidance only.
    /// </summary>
    GlobalLibrary = 1
}

/// <summary>
/// Represents generated ADR draft content for a free-text question.
/// </summary>
public sealed record AdrQuestionDraftResult
{
    /// <summary>
    /// Gets the context mode used for generation.
    /// </summary>
    public required AdrQuestionContextMode ContextMode { get; init; }

    /// <summary>
    /// Gets the normalized user question.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets the suggested ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the suggested ADR slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the synthesized problem statement.
    /// </summary>
    public required string ProblemStatement { get; init; }

    /// <summary>
    /// Gets considered options with explicit pros and cons.
    /// </summary>
    public required IReadOnlyList<AdrDecisionOptionViewModel> ConsideredOptions { get; init; }

    /// <summary>
    /// Gets the suggested option for manual review.
    /// </summary>
    public required string SuggestedOption { get; init; }

    /// <summary>
    /// Gets the rationale for the suggested option.
    /// </summary>
    public required string SuggestedOptionRationale { get; init; }

    /// <summary>
    /// Gets markdown body content for a proposed ADR draft.
    /// </summary>
    public required string DraftMarkdownBody { get; init; }

    /// <summary>
    /// Gets the generated ADR lifecycle status. This is always proposed.
    /// </summary>
    public AdrStatus DraftStatus { get; init; } = AdrStatus.Proposed;

    /// <summary>
    /// Gets the raw recommendation payload used to build the draft.
    /// </summary>
    public required AdrEvaluationRecommendation Recommendation { get; init; }
}
