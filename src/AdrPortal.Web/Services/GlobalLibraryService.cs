using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;

namespace AdrPortal.Web.Services;

/// <summary>
/// Provides global ADR library projections and sync workflow scaffolding.
/// </summary>
public sealed class GlobalLibraryService(
    IGlobalAdrStore globalAdrStore,
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IGitPrWorkflowQueue? gitPrWorkflowQueue = null,
    IGitPrWorkflowProcessor? gitPrWorkflowProcessor = null)
{
    private const string DefaultBaseBranchName = "master";
    /// <summary>
    /// Gets projected overview data for the global ADR library.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Global library overview projection.</returns>
    public async Task<GlobalLibraryOverviewProjection> GetOverviewAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var globalAdrs = await globalAdrStore.GetAllAsync(ct);
        var items = new List<GlobalLibraryOverviewItem>(globalAdrs.Count);
        foreach (var globalAdr in globalAdrs)
        {
            ct.ThrowIfCancellationRequested();
            var instances = await globalAdrStore.GetInstancesAsync(globalAdr.GlobalId, ct);
            var proposals = await globalAdrStore.GetUpdateProposalsAsync(globalAdr.GlobalId, ct);
            items.Add(
                new GlobalLibraryOverviewItem
                {
                    GlobalId = globalAdr.GlobalId,
                    Title = globalAdr.Title,
                    CurrentVersion = globalAdr.CurrentVersion,
                    InstanceCount = instances.Count,
                    UpdateAvailableCount = instances.Count(instance => instance.UpdateAvailable),
                    PendingProposalCount = proposals.Count(proposal => proposal.IsPending)
                });
        }

        var dashboardPendingCount = await globalAdrStore.GetDashboardPendingCountAsync(ct);
        return new GlobalLibraryOverviewProjection
        {
            Items = items
                .OrderByDescending(item => item.PendingProposalCount + item.UpdateAvailableCount)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.GlobalId)
                .ToArray(),
            DashboardPendingCount = dashboardPendingCount
        };
    }

    /// <summary>
    /// Gets detailed projection data for a single global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Detail projection when found; otherwise <see langword="null"/>.</returns>
    public async Task<GlobalAdrDetailProjection?> GetDetailAsync(Guid globalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var globalAdr = await globalAdrStore.GetByIdAsync(globalId, ct);
        if (globalAdr is null)
        {
            return null;
        }

        var versions = await globalAdrStore.GetVersionsAsync(globalId, ct);
        var proposals = await globalAdrStore.GetUpdateProposalsAsync(globalId, ct);
        var instances = await globalAdrStore.GetInstancesAsync(globalId, ct);
        var repositoryIds = instances.Select(instance => instance.RepositoryId)
            .Concat(proposals.Select(proposal => proposal.RepositoryId))
            .Distinct()
            .ToArray();
        var repositoryById = await managedRepositoryStore.GetByIdsAsync(repositoryIds, ct);

        var versionViewModels = versions
            .OrderByDescending(version => version.VersionNumber)
            .Select(
                version => new GlobalAdrVersionViewModel
                {
                    Id = version.Id,
                    GlobalId = version.GlobalId,
                    VersionNumber = version.VersionNumber,
                    Title = version.Title,
                    MarkdownContent = version.MarkdownContent,
                    CreatedAtUtc = version.CreatedAtUtc
                })
            .ToArray();

        var proposalViewModels = proposals
            .Select(
                proposal => new GlobalAdrUpdateProposalViewModel
                {
                    Id = proposal.Id,
                    GlobalId = proposal.GlobalId,
                    RepositoryId = proposal.RepositoryId,
                    RepositoryDisplayName = ResolveRepositoryDisplayName(repositoryById, proposal.RepositoryId),
                    LocalAdrNumber = proposal.LocalAdrNumber,
                    ProposedFromVersion = proposal.ProposedFromVersion,
                    ProposedTitle = proposal.ProposedTitle,
                    ProposedMarkdownContent = proposal.ProposedMarkdownContent,
                    IsPending = proposal.IsPending,
                    CreatedAtUtc = proposal.CreatedAtUtc
                })
            .OrderByDescending(proposal => proposal.IsPending)
            .ThenByDescending(proposal => proposal.CreatedAtUtc)
            .ThenBy(proposal => proposal.RepositoryDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(proposal => proposal.LocalAdrNumber)
            .ToArray();

        var instanceViewModels = instances
            .Select(
                instance => new GlobalAdrInstanceViewModel
                {
                    Id = instance.Id,
                    GlobalId = instance.GlobalId,
                    RepositoryId = instance.RepositoryId,
                    RepositoryDisplayName = ResolveRepositoryDisplayName(repositoryById, instance.RepositoryId),
                    LocalAdrNumber = instance.LocalAdrNumber,
                    RepoRelativePath = instance.RepoRelativePath,
                    LastKnownStatus = instance.LastKnownStatus,
                    LastKnownStatusView = AdrStatusViewModel.FromStatus(instance.LastKnownStatus),
                    BaseTemplateVersion = instance.BaseTemplateVersion,
                    HasLocalChanges = instance.HasLocalChanges,
                    UpdateAvailable = instance.UpdateAvailable,
                    LastReviewedAtUtc = instance.LastReviewedAtUtc
                })
            .OrderBy(instance => instance.RepositoryDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(instance => instance.LocalAdrNumber)
            .ToArray();

        var baselineDiff = BuildBaselineDiff(versionViewModels);

        return new GlobalAdrDetailProjection
        {
            GlobalId = globalAdr.GlobalId,
            Title = globalAdr.Title,
            CurrentVersion = globalAdr.CurrentVersion,
            RegisteredAtUtc = globalAdr.RegisteredAtUtc,
            LastUpdatedAtUtc = globalAdr.LastUpdatedAtUtc,
            Versions = versionViewModels,
            Proposals = proposalViewModels,
            Instances = instanceViewModels,
            BaselineDiff = baselineDiff,
            PendingProposalCount = proposalViewModels.Count(proposal => proposal.IsPending),
            UpdateAvailableCount = instanceViewModels.Count(instance => instance.UpdateAvailable)
        };
    }

    /// <summary>
    /// Reconciles one repository against known global ADR links found in markdown front matter.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Mapping count created or updated.</returns>
    public async Task<int> ReconcileRepoAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            throw new InvalidOperationException($"Repository '{repositoryId}' was not found.");
        }

        var adrRepository = madrRepositoryFactory.Create(repository);
        var adrs = await adrRepository.GetAllAsync(ct);
        var reconciledCount = 0;
        foreach (var adr in adrs.Where(adr => adr.GlobalId is not null && adr.GlobalVersion is not null))
        {
            ct.ThrowIfCancellationRequested();

            var globalId = adr.GlobalId!.Value;
            var globalVersion = adr.GlobalVersion!.Value;
            if (globalVersion < 1)
            {
                continue;
            }

            var globalAdr = await globalAdrStore.GetByIdAsync(globalId, ct);
            if (globalAdr is null)
            {
                continue;
            }

            var version = await ResolveVersionAsync(globalId, globalVersion, ct);
            var hasLocalChanges = version is null || !string.Equals(
                NormalizeMarkdown(adr.RawMarkdown),
                NormalizeMarkdown(version.MarkdownContent),
                StringComparison.Ordinal);

            _ = await globalAdrStore.UpsertInstanceAsync(
                new GlobalAdrInstance
                {
                    GlobalId = globalId,
                    RepositoryId = repository.Id,
                    LocalAdrNumber = adr.Number,
                    RepoRelativePath = adr.RepoRelativePath,
                    LastKnownStatus = adr.Status,
                    BaseTemplateVersion = globalVersion,
                    HasLocalChanges = hasLocalChanges,
                    UpdateAvailable = globalAdr.CurrentVersion > globalVersion,
                    LastReviewedAtUtc = DateTime.UtcNow
                },
                ct);

            reconciledCount++;
        }

        return reconciledCount;
    }

    /// <summary>
    /// Computes sync status for a repository ADR linked to the global library.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="adr">ADR value.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Sync status when linked; otherwise <see langword="null"/>.</returns>
    public async Task<RepositoryAdrSyncStatus?> GetRepositoryAdrSyncStatusAsync(int repositoryId, Adr adr, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(adr);
        ct.ThrowIfCancellationRequested();

        if (adr.GlobalId is null || adr.GlobalVersion is null || adr.GlobalVersion.Value < 1)
        {
            return null;
        }

        var globalAdr = await globalAdrStore.GetByIdAsync(adr.GlobalId.Value, ct);
        if (globalAdr is null)
        {
            return null;
        }

        var currentGlobalVersion = globalAdr.CurrentVersion;
        var baseVersion = adr.GlobalVersion.Value;
        var baseTemplate = await ResolveVersionAsync(globalAdr.GlobalId, baseVersion, ct);
        var hasLocalChanges = baseTemplate is null || !string.Equals(
            NormalizeMarkdown(adr.RawMarkdown),
            NormalizeMarkdown(baseTemplate.MarkdownContent),
            StringComparison.Ordinal);
        var updateAvailable = currentGlobalVersion > baseVersion;

        var proposals = await globalAdrStore.GetUpdateProposalsAsync(globalAdr.GlobalId, ct);
        var hasPendingProposal = proposals.Any(
            proposal => proposal.IsPending
                && proposal.RepositoryId == repositoryId
                && proposal.LocalAdrNumber == adr.Number);

        return new RepositoryAdrSyncStatus
        {
            GlobalId = globalAdr.GlobalId,
            RepositoryId = repositoryId,
            LocalAdrNumber = adr.Number,
            BaseTemplateVersion = baseVersion,
            CurrentGlobalVersion = currentGlobalVersion,
            HasLocalChanges = hasLocalChanges,
            UpdateAvailable = updateAvailable,
            HasPendingProposal = hasPendingProposal
        };
    }

    /// <summary>
    /// Creates a pending proposal from a repository ADR when local changes exist.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="adr">Repository ADR value.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome message for the scaffolding flow.</returns>
    public async Task<GlobalSyncActionResult> ProposeLibraryUpdateAsync(int repositoryId, Adr adr, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(adr);
        ct.ThrowIfCancellationRequested();

        var syncStatus = await GetRepositoryAdrSyncStatusAsync(repositoryId, adr, ct);
        if (syncStatus is null)
        {
            throw new InvalidOperationException("ADR is not linked to a global template.");
        }

        if (!syncStatus.HasLocalChanges)
        {
            throw new InvalidOperationException("ADR has no local changes to propose.");
        }

        if (syncStatus.HasPendingProposal)
        {
            return new GlobalSyncActionResult
            {
                Message = $"A pending proposal already exists for ADR-{adr.Number:0000}."
            };
        }

        _ = await globalAdrStore.AddUpdateProposalAsync(
            new GlobalAdrUpdateProposal
            {
                GlobalId = syncStatus.GlobalId,
                RepositoryId = repositoryId,
                LocalAdrNumber = adr.Number,
                ProposedFromVersion = syncStatus.BaseTemplateVersion,
                ProposedTitle = adr.Title,
                ProposedMarkdownContent = adr.RawMarkdown,
                IsPending = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            ct);

        return new GlobalSyncActionResult
        {
            Message = $"Created a pending library update proposal from ADR-{adr.Number:0000}."
        };
    }

    /// <summary>
    /// Accepts a pending proposal and materializes a new global template version.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="proposalId">Proposal identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome message.</returns>
    public async Task<GlobalSyncActionResult> AcceptLibraryProposalAsync(Guid globalId, int proposalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var globalAdr = await globalAdrStore.GetByIdAsync(globalId, ct);
        if (globalAdr is null)
        {
            throw new InvalidOperationException($"Global ADR '{globalId}' was not found.");
        }

        var proposals = await globalAdrStore.GetUpdateProposalsAsync(globalId, ct);
        var proposal = proposals.SingleOrDefault(candidate => candidate.Id == proposalId);
        if (proposal is null)
        {
            throw new InvalidOperationException($"Proposal '{proposalId}' was not found.");
        }

        if (!proposal.IsPending)
        {
            return new GlobalSyncActionResult
            {
                Message = $"Proposal {proposalId} was already reviewed."
            };
        }

        var newVersion = globalAdr.CurrentVersion + 1;
        _ = await globalAdrStore.AddVersionAsync(
            new GlobalAdrVersion
            {
                GlobalId = globalId,
                VersionNumber = newVersion,
                Title = proposal.ProposedTitle,
                MarkdownContent = proposal.ProposedMarkdownContent,
                CreatedAtUtc = DateTime.UtcNow
            },
            ct);

        _ = await globalAdrStore.UpdateAsync(
            new GlobalAdr
            {
                GlobalId = globalAdr.GlobalId,
                Title = proposal.ProposedTitle,
                CurrentVersion = newVersion,
                RegisteredAtUtc = globalAdr.RegisteredAtUtc,
                LastUpdatedAtUtc = DateTime.UtcNow,
                Instances = [],
                UpdateProposals = [],
                Versions = []
            },
            ct);

        _ = await globalAdrStore.UpdateProposalAsync(
            CopyProposal(proposal, isPending: false),
            ct);

        var instances = await globalAdrStore.GetInstancesAsync(globalId, ct);
        foreach (var instance in instances.Where(instance => instance.RepositoryId != proposal.RepositoryId || instance.LocalAdrNumber != proposal.LocalAdrNumber))
        {
            ct.ThrowIfCancellationRequested();
            _ = await globalAdrStore.UpsertInstanceAsync(
                CopyInstance(
                    instance,
                    baseTemplateVersion: instance.BaseTemplateVersion,
                    hasLocalChanges: instance.HasLocalChanges,
                    updateAvailable: true,
                    lastReviewedAtUtc: DateTime.UtcNow),
                ct);
        }

        return new GlobalSyncActionResult
        {
            Message = $"Accepted proposal {proposalId} and published global version v{newVersion}."
        };
    }

    /// <summary>
    /// Discards a pending proposal.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="proposalId">Proposal identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome message.</returns>
    public async Task<GlobalSyncActionResult> DiscardLibraryProposalAsync(Guid globalId, int proposalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var proposals = await globalAdrStore.GetUpdateProposalsAsync(globalId, ct);
        var proposal = proposals.SingleOrDefault(candidate => candidate.Id == proposalId);
        if (proposal is null)
        {
            throw new InvalidOperationException($"Proposal '{proposalId}' was not found.");
        }

        if (!proposal.IsPending)
        {
            return new GlobalSyncActionResult
            {
                Message = $"Proposal {proposalId} was already reviewed."
            };
        }

        _ = await globalAdrStore.UpdateProposalAsync(
            CopyProposal(proposal, isPending: false),
            ct);

        return new GlobalSyncActionResult
        {
            Message = $"Discarded proposal {proposalId}."
        };
    }

    /// <summary>
    /// Marks an instance update as reviewed and applied.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="instanceId">Instance identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome message for scaffolding.</returns>
    public async Task<GlobalSyncActionResult> ApplyUpdateToInstanceAsync(Guid globalId, int instanceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var instance = await ResolveInstanceAsync(globalId, instanceId, ct);

        var globalAdr = await globalAdrStore.GetByIdAsync(globalId, ct)
            ?? throw new InvalidOperationException($"Global ADR '{globalId}' was not found.");

        var repository = await managedRepositoryStore.GetByIdAsync(instance.RepositoryId, ct)
            ?? throw new InvalidOperationException($"Repository '{instance.RepositoryId}' was not found.");
        var repositoryAdrStore = madrRepositoryFactory.Create(repository);
        var adr = await repositoryAdrStore.GetByNumberAsync(instance.LocalAdrNumber, ct)
            ?? throw new InvalidOperationException(
                $"ADR-{instance.LocalAdrNumber:0000} was not found in repository '{repository.DisplayName}'.");
        var templateVersion = await ResolveVersionAsync(globalId, globalAdr.CurrentVersion, ct)
            ?? throw new InvalidOperationException(
                $"Global ADR '{globalId}' version '{globalAdr.CurrentVersion}' is not available.");

        var updatedAdr = adr with
        {
            Title = templateVersion.Title,
            RawMarkdown = templateVersion.MarkdownContent,
            GlobalVersion = templateVersion.VersionNumber,
            Status = AdrStatus.Accepted
        };
        var persistedAdr = await repositoryAdrStore.WriteAsync(updatedAdr, ct);
        await QueueAndProcessAsync(repository, persistedAdr, ct);

        _ = await globalAdrStore.UpsertInstanceAsync(
            CopyInstance(
                instance,
                baseTemplateVersion: globalAdr.CurrentVersion,
                hasLocalChanges: false,
                updateAvailable: false,
                lastReviewedAtUtc: DateTime.UtcNow),
            ct);

        return new GlobalSyncActionResult
        {
            Message = $"Marked ADR-{instance.LocalAdrNumber:0000} as updated to v{globalAdr.CurrentVersion}."
        };
    }

    /// <summary>
    /// Marks an instance as custom updated while preserving local changes.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="instanceId">Instance identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome message for scaffolding.</returns>
    public async Task<GlobalSyncActionResult> CustomiseInstanceUpdateAsync(Guid globalId, int instanceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var instance = await ResolveInstanceAsync(globalId, instanceId, ct);

        var globalAdr = await globalAdrStore.GetByIdAsync(globalId, ct)
            ?? throw new InvalidOperationException($"Global ADR '{globalId}' was not found.");

        _ = await globalAdrStore.UpsertInstanceAsync(
            CopyInstance(
                instance,
                baseTemplateVersion: globalAdr.CurrentVersion,
                hasLocalChanges: true,
                updateAvailable: false,
                lastReviewedAtUtc: DateTime.UtcNow),
            ct);

        return new GlobalSyncActionResult
        {
            Message = $"Marked ADR-{instance.LocalAdrNumber:0000} as customised against v{globalAdr.CurrentVersion}."
        };
    }

    /// <summary>
    /// Dismisses an instance update notification without changing base version.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="instanceId">Instance identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome message for scaffolding.</returns>
    public async Task<GlobalSyncActionResult> DismissInstanceUpdateAsync(Guid globalId, int instanceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var instance = await ResolveInstanceAsync(globalId, instanceId, ct);

        _ = await globalAdrStore.UpsertInstanceAsync(
            CopyInstance(
                instance,
                baseTemplateVersion: instance.BaseTemplateVersion,
                hasLocalChanges: instance.HasLocalChanges,
                updateAvailable: false,
                lastReviewedAtUtc: DateTime.UtcNow),
            ct);

        return new GlobalSyncActionResult
        {
            Message = $"Dismissed update notification for ADR-{instance.LocalAdrNumber:0000}."
        };
    }

    private async Task<GlobalAdrInstance> ResolveInstanceAsync(Guid globalId, int instanceId, CancellationToken ct)
    {
        var instances = await globalAdrStore.GetInstancesAsync(globalId, ct);
        var instance = instances.SingleOrDefault(candidate => candidate.Id == instanceId);
        if (instance is null)
        {
            throw new InvalidOperationException($"Instance '{instanceId}' was not found for global ADR '{globalId}'.");
        }

        return instance;
    }

    private async Task<GlobalAdrVersion?> ResolveVersionAsync(Guid globalId, int versionNumber, CancellationToken ct)
    {
        var versions = await globalAdrStore.GetVersionsAsync(globalId, ct);
        return versions.SingleOrDefault(version => version.VersionNumber == versionNumber);
    }

    private static string ResolveRepositoryDisplayName(IReadOnlyDictionary<int, ManagedRepository> repositoriesById, int repositoryId)
    {
        if (repositoriesById.TryGetValue(repositoryId, out var repository))
        {
            return repository.DisplayName;
        }

        return $"Repository {repositoryId}";
    }

    private static string BuildBaselineDiff(IReadOnlyList<GlobalAdrVersionViewModel> versions)
    {
        if (versions.Count < 2)
        {
            return "Baseline diff unavailable until at least two versions exist.";
        }

        var latest = versions[0];
        var previous = versions[1];

        var latestLines = SplitLines(NormalizeMarkdown(latest.MarkdownContent));
        var previousLines = SplitLines(NormalizeMarkdown(previous.MarkdownContent));
        var maxLines = Math.Max(latestLines.Length, previousLines.Length);
        var diff = new List<string>
        {
            $"Baseline diff: v{previous.VersionNumber} -> v{latest.VersionNumber}"
        };

        for (var index = 0; index < maxLines; index++)
        {
            var previousLine = index < previousLines.Length ? previousLines[index] : null;
            var latestLine = index < latestLines.Length ? latestLines[index] : null;

            if (string.Equals(previousLine, latestLine, StringComparison.Ordinal))
            {
                continue;
            }

            if (previousLine is not null)
            {
                diff.Add($"- {previousLine}");
            }

            if (latestLine is not null)
            {
                diff.Add($"+ {latestLine}");
            }
        }

        if (diff.Count == 1)
        {
            diff.Add("No textual changes detected.");
        }

        return string.Join(Environment.NewLine, diff);
    }

    private static string[] SplitLines(string markdown)
    {
        return markdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static string NormalizeMarkdown(string markdown)
    {
        return markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static GlobalAdrInstance CopyInstance(
        GlobalAdrInstance source,
        int baseTemplateVersion,
        bool hasLocalChanges,
        bool updateAvailable,
        DateTime lastReviewedAtUtc)
    {
        return new GlobalAdrInstance
        {
            Id = source.Id,
            GlobalId = source.GlobalId,
            RepositoryId = source.RepositoryId,
            LocalAdrNumber = source.LocalAdrNumber,
            RepoRelativePath = source.RepoRelativePath,
            LastKnownStatus = source.LastKnownStatus,
            BaseTemplateVersion = baseTemplateVersion,
            HasLocalChanges = hasLocalChanges,
            UpdateAvailable = updateAvailable,
            LastReviewedAtUtc = lastReviewedAtUtc
        };
    }

    private static GlobalAdrUpdateProposal CopyProposal(GlobalAdrUpdateProposal source, bool isPending)
    {
        return new GlobalAdrUpdateProposal
        {
            Id = source.Id,
            GlobalId = source.GlobalId,
            RepositoryId = source.RepositoryId,
            LocalAdrNumber = source.LocalAdrNumber,
            ProposedFromVersion = source.ProposedFromVersion,
            ProposedTitle = source.ProposedTitle,
            ProposedMarkdownContent = source.ProposedMarkdownContent,
            IsPending = isPending,
            CreatedAtUtc = source.CreatedAtUtc
        };
    }

    private async Task QueueAndProcessAsync(ManagedRepository repository, Adr adr, CancellationToken ct)
    {
        if (gitPrWorkflowQueue is null)
        {
            return;
        }

        var queueItem = await BuildQueueItemAsync(repository, adr, ct);
        await gitPrWorkflowQueue.EnqueueAsync(queueItem, ct);
        if (gitPrWorkflowProcessor is not null)
        {
            _ = await gitPrWorkflowProcessor.ProcessAndUpdateQueueAsync(queueItem.Id, ct);
        }
    }

    private async Task<GitPrWorkflowQueueItem> BuildQueueItemAsync(ManagedRepository repository, Adr adr, CancellationToken ct)
    {
        var branchName = $"proposed/adr-{adr.Number:0000}-{adr.Slug}";
        var pullRequestUrl = default(Uri);
        var pullRequestNumber = default(int?);
        var commitSha = default(string);

        var previousQueueItem = await gitPrWorkflowQueue!.GetLatestForAdrAsync(repository.Id, adr.Number, ct);
        if (previousQueueItem is not null)
        {
            branchName = previousQueueItem.BranchName;
            pullRequestUrl = previousQueueItem.PullRequestUrl;
            pullRequestNumber = previousQueueItem.PullRequestNumber;
            commitSha = previousQueueItem.CommitSha;
        }

        return new GitPrWorkflowQueueItem
        {
            RepositoryId = repository.Id,
            RepositoryDisplayName = repository.DisplayName,
            RepositoryRootPath = repository.RootPath,
            RepositoryRemoteUrl = ResolveRepositoryRemoteUrl(repository.GitRemoteUrl),
            RepoRelativePath = adr.RepoRelativePath,
            AdrNumber = adr.Number,
            AdrSlug = adr.Slug,
            AdrTitle = adr.Title,
            AdrStatus = adr.Status.ToString(),
            Trigger = GitPrWorkflowTrigger.GlobalLibraryApply,
            Action = GitPrWorkflowAction.CreateOrUpdatePullRequest,
            BranchName = branchName,
            BaseBranchName = DefaultBaseBranchName,
            PullRequestUrl = pullRequestUrl,
            PullRequestNumber = pullRequestNumber,
            CommitSha = commitSha,
            EnqueuedAtUtc = DateTime.UtcNow
        };
    }

    private static string ResolveRepositoryRemoteUrl(string? gitRemoteUrl)
    {
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
        {
            throw new InvalidOperationException(
                "Git remote URL is required to run Git/PR workflow automation. Configure a GitHub remote in repository settings.");
        }

        return gitRemoteUrl.Trim();
    }
}
