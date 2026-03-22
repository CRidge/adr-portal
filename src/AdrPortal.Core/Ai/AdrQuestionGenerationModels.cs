namespace AdrPortal.Core.Ai;

/// <summary>
/// Represents AI-generated ADR draft scaffolding from a repository question prompt.
/// </summary>
public sealed record AdrQuestionGenerationResult
{
    /// <summary>
    /// Gets the original question prompt entered by the user.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets the suggested ADR title derived from the question context.
    /// </summary>
    public required string SuggestedTitle { get; init; }

    /// <summary>
    /// Gets the suggested ADR slug derived from the question context.
    /// </summary>
    public required string SuggestedSlug { get; init; }

    /// <summary>
    /// Gets the generated problem statement used to seed the ADR draft.
    /// </summary>
    public required string ProblemStatement { get; init; }

    /// <summary>
    /// Gets generated decision drivers derived from the question and repository constraints.
    /// </summary>
    public IReadOnlyList<string> DecisionDrivers { get; init; } = [];

    /// <summary>
    /// Gets the generated recommendation containing options, pros/cons, and preferred option.
    /// </summary>
    public required AdrEvaluationRecommendation Recommendation { get; init; }
}
