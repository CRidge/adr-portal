namespace AdrPortal.Core.Ai;

/// <summary>
/// Defines an abstraction boundary for AI-assisted ADR workflows.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Evaluates a draft ADR against existing ADRs and returns a structured recommendation.
    /// </summary>
    /// <param name="draftAdr">Draft ADR content under analysis.</param>
    /// <param name="existingAdrs">Existing ADR corpus used for grounding.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured recommendation output for user review.</returns>
    Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        CancellationToken ct);

    /// <summary>
    /// Finds existing ADRs potentially affected by a new or edited draft ADR.
    /// </summary>
    /// <param name="draftAdr">Draft ADR content under analysis.</param>
    /// <param name="existingAdrs">Existing ADR corpus used for grounding.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured affected ADR analysis output for user review.</returns>
    Task<AffectedAdrAnalysisResult> FindAffectedAdrsAsync(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        CancellationToken ct);

    /// <summary>
    /// Generates ADR draft guidance from a user question in repository context.
    /// </summary>
    /// <param name="question">User question used to seed ADR generation.</param>
    /// <param name="existingAdrs">Existing ADR corpus used for grounding and constraints.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Generated ADR draft guidance including recommendation options.</returns>
    Task<AdrQuestionGenerationResult> GenerateDraftFromQuestionAsync(
        string question,
        IReadOnlyList<AdrPortal.Core.Entities.Adr> existingAdrs,
        CancellationToken ct);

    /// <summary>
    /// Scans a codebase and returns structured ADR bootstrap proposals.
    /// </summary>
    /// <param name="repoRootPath">Absolute repository root path to analyze.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured proposal set for user review.</returns>
    Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct);
}
