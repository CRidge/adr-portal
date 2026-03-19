namespace AdrPortal.Core.Ai;

/// <summary>
/// Defines an abstraction boundary for AI-assisted ADR workflows.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Scans a codebase and returns structured ADR bootstrap proposals.
    /// </summary>
    /// <param name="repoRootPath">Absolute repository root path to analyze.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured proposal set for user review.</returns>
    Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct);
}
