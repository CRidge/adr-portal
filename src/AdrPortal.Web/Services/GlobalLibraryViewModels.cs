using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Represents one row in the global library overview screen.
/// </summary>
public sealed record GlobalLibraryOverviewItem
{
    /// <summary>
    /// Gets the global ADR identifier.
    /// </summary>
    public required Guid GlobalId { get; init; }

    /// <summary>
    /// Gets the global ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the latest template version number.
    /// </summary>
    public required int CurrentVersion { get; init; }

    /// <summary>
    /// Gets the count of linked repository instances.
    /// </summary>
    public required int InstanceCount { get; init; }

    /// <summary>
    /// Gets the count of linked repository instances with update available.
    /// </summary>
    public required int UpdateAvailableCount { get; init; }

    /// <summary>
    /// Gets the count of pending update proposals.
    /// </summary>
    public required int PendingProposalCount { get; init; }
}

/// <summary>
/// Represents the global library overview projection.
/// </summary>
public sealed record GlobalLibraryOverviewProjection
{
    /// <summary>
    /// Gets the projected overview rows.
    /// </summary>
    public required IReadOnlyList<GlobalLibraryOverviewItem> Items { get; init; }

    /// <summary>
    /// Gets the dashboard badge count for pending global sync work.
    /// </summary>
    public required int DashboardPendingCount { get; init; }
}

/// <summary>
/// Represents one global ADR version row for detail rendering.
/// </summary>
public sealed record GlobalAdrVersionViewModel
{
    /// <summary>
    /// Gets the version row identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the global ADR identifier.
    /// </summary>
    public required Guid GlobalId { get; init; }

    /// <summary>
    /// Gets the version number.
    /// </summary>
    public required int VersionNumber { get; init; }

    /// <summary>
    /// Gets the version title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the markdown content.
    /// </summary>
    public required string MarkdownContent { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the version was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Represents one pending or historical update proposal.
/// </summary>
public sealed record GlobalAdrUpdateProposalViewModel
{
    /// <summary>
    /// Gets the proposal identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the global ADR identifier.
    /// </summary>
    public required Guid GlobalId { get; init; }

    /// <summary>
    /// Gets the source repository identifier.
    /// </summary>
    public required int RepositoryId { get; init; }

    /// <summary>
    /// Gets the source repository display name.
    /// </summary>
    public required string RepositoryDisplayName { get; init; }

    /// <summary>
    /// Gets the source ADR number.
    /// </summary>
    public required int LocalAdrNumber { get; init; }

    /// <summary>
    /// Gets the base template version for the proposal.
    /// </summary>
    public required int ProposedFromVersion { get; init; }

    /// <summary>
    /// Gets the proposed title.
    /// </summary>
    public required string ProposedTitle { get; init; }

    /// <summary>
    /// Gets the proposed markdown payload.
    /// </summary>
    public required string ProposedMarkdownContent { get; init; }

    /// <summary>
    /// Gets whether the proposal remains pending.
    /// </summary>
    public required bool IsPending { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when proposal was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Represents one repository instance row in the global ADR detail page.
/// </summary>
public sealed record GlobalAdrInstanceViewModel
{
    /// <summary>
    /// Gets the instance identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the global ADR identifier.
    /// </summary>
    public required Guid GlobalId { get; init; }

    /// <summary>
    /// Gets the repository identifier.
    /// </summary>
    public required int RepositoryId { get; init; }

    /// <summary>
    /// Gets the repository display name.
    /// </summary>
    public required string RepositoryDisplayName { get; init; }

    /// <summary>
    /// Gets the ADR number within the repository.
    /// </summary>
    public required int LocalAdrNumber { get; init; }

    /// <summary>
    /// Gets the repository-relative markdown path.
    /// </summary>
    public required string RepoRelativePath { get; init; }

    /// <summary>
    /// Gets the latest known ADR status.
    /// </summary>
    public required AdrStatus LastKnownStatus { get; init; }

    /// <summary>
    /// Gets status badge metadata.
    /// </summary>
    public required AdrStatusViewModel LastKnownStatusView { get; init; }

    /// <summary>
    /// Gets the base template version this instance last reviewed.
    /// </summary>
    public required int BaseTemplateVersion { get; init; }

    /// <summary>
    /// Gets whether repository markdown diverged from base template.
    /// </summary>
    public required bool HasLocalChanges { get; init; }

    /// <summary>
    /// Gets whether a newer global template version is available.
    /// </summary>
    public required bool UpdateAvailable { get; init; }

    /// <summary>
    /// Gets the last reviewed UTC timestamp.
    /// </summary>
    public required DateTime LastReviewedAtUtc { get; init; }
}

/// <summary>
/// Represents the global ADR detail projection.
/// </summary>
public sealed record GlobalAdrDetailProjection
{
    /// <summary>
    /// Gets the global ADR identifier.
    /// </summary>
    public required Guid GlobalId { get; init; }

    /// <summary>
    /// Gets the global ADR title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the current template version.
    /// </summary>
    public required int CurrentVersion { get; init; }

    /// <summary>
    /// Gets the initial registration timestamp.
    /// </summary>
    public required DateTime RegisteredAtUtc { get; init; }

    /// <summary>
    /// Gets the last update timestamp.
    /// </summary>
    public required DateTime LastUpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets version history rows.
    /// </summary>
    public required IReadOnlyList<GlobalAdrVersionViewModel> Versions { get; init; }

    /// <summary>
    /// Gets update proposal rows.
    /// </summary>
    public required IReadOnlyList<GlobalAdrUpdateProposalViewModel> Proposals { get; init; }

    /// <summary>
    /// Gets repository instance rows.
    /// </summary>
    public required IReadOnlyList<GlobalAdrInstanceViewModel> Instances { get; init; }

    /// <summary>
    /// Gets baseline diff text between template versions.
    /// </summary>
    public required string BaselineDiff { get; init; }

    /// <summary>
    /// Gets count of pending proposals.
    /// </summary>
    public required int PendingProposalCount { get; init; }

    /// <summary>
    /// Gets count of update-available instances.
    /// </summary>
    public required int UpdateAvailableCount { get; init; }
}

/// <summary>
/// Represents repository ADR sync status computed against the global library.
/// </summary>
public sealed record RepositoryAdrSyncStatus
{
    /// <summary>
    /// Gets the linked global ADR identifier.
    /// </summary>
    public required Guid GlobalId { get; init; }

    /// <summary>
    /// Gets the repository identifier.
    /// </summary>
    public required int RepositoryId { get; init; }

    /// <summary>
    /// Gets the repository ADR number.
    /// </summary>
    public required int LocalAdrNumber { get; init; }

    /// <summary>
    /// Gets the instance base template version.
    /// </summary>
    public required int BaseTemplateVersion { get; init; }

    /// <summary>
    /// Gets the current global template version.
    /// </summary>
    public required int CurrentGlobalVersion { get; init; }

    /// <summary>
    /// Gets whether repository markdown diverged from the base template.
    /// </summary>
    public required bool HasLocalChanges { get; init; }

    /// <summary>
    /// Gets whether a newer template version exists.
    /// </summary>
    public required bool UpdateAvailable { get; init; }

    /// <summary>
    /// Gets whether a pending proposal already exists for this ADR instance.
    /// </summary>
    public required bool HasPendingProposal { get; init; }
}

/// <summary>
/// Represents a simple outcome message for sync workflow actions.
/// </summary>
public sealed record GlobalSyncActionResult
{
    /// <summary>
    /// Gets the user-facing outcome message.
    /// </summary>
    public required string Message { get; init; }
}
