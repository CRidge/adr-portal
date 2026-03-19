namespace AdrPortal.Core.Ai;

/// <summary>
/// Represents one normalized chunk extracted from scanned repository content.
/// </summary>
public sealed record CodebaseScanChunk
{
    /// <summary>
    /// Gets the stable chunk identifier.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Gets the repository-relative path that produced this chunk.
    /// </summary>
    public required string RepoRelativePath { get; init; }

    /// <summary>
    /// Gets a deterministic plain-text preview from the chunk.
    /// </summary>
    public required string Preview { get; init; }

    /// <summary>
    /// Gets the number of characters in the preview.
    /// </summary>
    public required int CharacterCount { get; init; }
}

/// <summary>
/// Represents one AI bootstrap ADR proposal produced from codebase evidence.
/// </summary>
public sealed record AdrDraftProposal
{
    /// <summary>
    /// Gets the stable proposal identifier.
    /// </summary>
    public required string ProposalId { get; init; }

    /// <summary>
    /// Gets the suggested ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the suggested filename slug for the ADR.
    /// </summary>
    public required string SuggestedSlug { get; init; }

    /// <summary>
    /// Gets the problem statement for the draft ADR.
    /// </summary>
    public required string ProblemStatement { get; init; }

    /// <summary>
    /// Gets key decision drivers inferred from the scanned codebase.
    /// </summary>
    public IReadOnlyList<string> DecisionDrivers { get; init; } = [];

    /// <summary>
    /// Gets initial options to seed ADR authoring.
    /// </summary>
    public IReadOnlyList<string> InitialOptions { get; init; } = [];

    /// <summary>
    /// Gets chunk identifiers that justify this proposal.
    /// </summary>
    public IReadOnlyList<string> EvidenceChunkIds { get; init; } = [];

    /// <summary>
    /// Gets repository-relative files referenced by this proposal.
    /// </summary>
    public IReadOnlyList<string> EvidenceFiles { get; init; } = [];

    /// <summary>
    /// Gets the deterministic confidence score in range [0.00, 1.00].
    /// </summary>
    public required double ConfidenceScore { get; init; }
}

/// <summary>
/// Represents the structured result returned from codebase ADR bootstrap analysis.
/// </summary>
public sealed record AdrBootstrapProposalSet
{
    /// <summary>
    /// Gets the normalized absolute repository root path used during scanning.
    /// </summary>
    public required string RepositoryRootPath { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when proposal generation completed.
    /// </summary>
    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    /// Gets the number of files scanned before chunking.
    /// </summary>
    public required int ScannedFileCount { get; init; }

    /// <summary>
    /// Gets extracted chunks from repository content.
    /// </summary>
    public required IReadOnlyList<CodebaseScanChunk> Chunks { get; init; }

    /// <summary>
    /// Gets generated ADR draft proposals.
    /// </summary>
    public required IReadOnlyList<AdrDraftProposal> Proposals { get; init; }
}
