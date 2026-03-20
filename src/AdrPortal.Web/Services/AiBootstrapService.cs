using System.Text;
using System.Text.RegularExpressions;
using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;

namespace AdrPortal.Web.Services;

/// <summary>
/// Provides phase-9 AI bootstrap workflows for empty repository ADR libraries.
/// </summary>
public sealed class AiBootstrapService(
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IAiService aiService,
    IGitPrWorkflowQueue gitPrWorkflowQueue,
    IGitPrWorkflowProcessor? gitPrWorkflowProcessor = null)
{
    private static readonly Regex SlugSanitizerRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private const string DefaultBaseBranchName = "master";

    /// <summary>
    /// Gets repository bootstrap context including whether ADR files already exist.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Bootstrap context when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<AiBootstrapContext?> GetContextAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = await madrRepositoryFactory.CreateAsync(repository, ct);
        var existingAdrs = await adrRepository.GetAllAsync(ct);
        return new AiBootstrapContext
        {
            Repository = repository,
            ExistingAdrCount = existingAdrs.Count
        };
    }

    /// <summary>
    /// Generates deterministic ADR bootstrap proposals for an empty repository.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured proposal set for user review.</returns>
    public async Task<AdrBootstrapProposalSet> GenerateProposalSetAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var context = await GetContextAsync(repositoryId, ct);
        if (context is null)
        {
            throw new InvalidOperationException($"Repository '{repositoryId}' was not found.");
        }

        if (context.ExistingAdrCount > 0)
        {
            throw new InvalidOperationException("AI bootstrap is available only when the repository has no ADRs.");
        }

        return await aiService.BootstrapAdrsFromCodebaseAsync(context.Repository.RootPath, ct);
    }

    /// <summary>
    /// Accepts selected proposals and writes proposed ADR markdown files.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="proposalSet">Previously generated proposal set.</param>
    /// <param name="selectedProposalIds">Selected proposal identifiers to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Outcome information for persisted ADRs and queued workflow items.</returns>
    public async Task<AiBootstrapAcceptResult> AcceptSelectedProposalsAsync(
        int repositoryId,
        AdrBootstrapProposalSet proposalSet,
        IReadOnlyCollection<string> selectedProposalIds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proposalSet);
        ArgumentNullException.ThrowIfNull(selectedProposalIds);
        ct.ThrowIfCancellationRequested();

        if (selectedProposalIds.Count is 0)
        {
            throw new InvalidOperationException("Select at least one AI proposal to create ADR drafts.");
        }

        var context = await GetContextAsync(repositoryId, ct);
        if (context is null)
        {
            throw new InvalidOperationException($"Repository '{repositoryId}' was not found.");
        }

        if (context.ExistingAdrCount > 0)
        {
            throw new InvalidOperationException("AI bootstrap is available only when the repository has no ADRs.");
        }

        var selectedById = new HashSet<string>(selectedProposalIds, StringComparer.Ordinal);
        var selectedProposals = proposalSet.Proposals
            .Where(proposal => selectedById.Contains(proposal.ProposalId))
            .OrderByDescending(proposal => proposal.ConfidenceScore)
            .ThenBy(proposal => proposal.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedProposals.Length != selectedById.Count)
        {
            throw new InvalidOperationException("One or more selected proposals could not be resolved.");
        }

        var adrRepository = await madrRepositoryFactory.CreateAsync(context.Repository, ct);
        var existingAdrs = await adrRepository.GetAllAsync(ct);
        var usedSlugs = existingAdrs
            .Select(adr => adr.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextNumber = await adrRepository.GetNextNumberAsync(ct);
        var createdAdrs = new List<Adr>(selectedProposals.Length);
        var queuedItems = new List<GitPrWorkflowQueueItem>(selectedProposals.Length);
        var normalizedAdrFolder = NormalizeRelativePath(context.Repository.AdrFolder);

        foreach (var proposal in selectedProposals)
        {
            ct.ThrowIfCancellationRequested();

            var uniqueSlug = CreateUniqueSlug(proposal.SuggestedSlug, usedSlugs);
            var adrNumber = nextNumber++;
            var repoRelativePath = $"{normalizedAdrFolder}/adr-{adrNumber:0000}-{uniqueSlug}.md";
            var draft = new Adr
            {
                Number = adrNumber,
                Slug = uniqueSlug,
                RepoRelativePath = repoRelativePath,
                Title = proposal.Title.Trim(),
                Status = AdrStatus.Proposed,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                DecisionMakers = ["ADR Portal AI Bootstrap"],
                Consulted = [],
                Informed = [],
                RawMarkdown = BuildBootstrapMarkdown(proposal)
            };

            var persisted = await adrRepository.WriteAsync(draft, ct);
            createdAdrs.Add(persisted);

            var queueItem = new GitPrWorkflowQueueItem
            {
                RepositoryId = context.Repository.Id,
                RepositoryDisplayName = context.Repository.DisplayName,
                RepositoryRootPath = context.Repository.RootPath,
                RepositoryRemoteUrl = ResolveRepositoryRemoteUrl(context.Repository.GitRemoteUrl),
                RepoRelativePath = persisted.RepoRelativePath,
                AdrNumber = persisted.Number,
                AdrSlug = persisted.Slug,
                AdrTitle = persisted.Title,
                AdrStatus = persisted.Status.ToString(),
                Trigger = GitPrWorkflowTrigger.AiBootstrap,
                Action = GitPrWorkflowAction.CreateOrUpdatePullRequest,
                BranchName = $"proposed/adr-{persisted.Number:0000}-{persisted.Slug}",
                BaseBranchName = DefaultBaseBranchName,
                EnqueuedAtUtc = DateTime.UtcNow
            };
            await gitPrWorkflowQueue.EnqueueAsync(queueItem, ct);
            var processedItem = queueItem;
            if (gitPrWorkflowProcessor is not null)
            {
                processedItem = await gitPrWorkflowProcessor.ProcessAndUpdateQueueAsync(queueItem.Id, ct);
            }

            queuedItems.Add(processedItem);
        }

        return new AiBootstrapAcceptResult
        {
            CreatedAdrs = createdAdrs,
            QueuedItems = queuedItems
        };
    }

    private static string BuildBootstrapMarkdown(AdrDraftProposal proposal)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {proposal.Title.Trim()}");
        builder.AppendLine();
        builder.AppendLine("## Context and Problem Statement");
        builder.AppendLine();
        builder.AppendLine(proposal.ProblemStatement.Trim());
        builder.AppendLine();
        builder.AppendLine("## Decision Drivers");
        builder.AppendLine();
        if (proposal.DecisionDrivers.Count is 0)
        {
            builder.AppendLine("- Clarify architectural direction and constraints.");
        }
        else
        {
            foreach (var decisionDriver in proposal.DecisionDrivers.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                builder.AppendLine($"- {decisionDriver.Trim()}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Considered Options");
        builder.AppendLine();
        if (proposal.InitialOptions.Count is 0)
        {
            builder.AppendLine("- Option 1");
            builder.AppendLine("- Option 2");
        }
        else
        {
            foreach (var option in proposal.InitialOptions.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                builder.AppendLine($"- {option.Trim()}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Decision Outcome");
        builder.AppendLine();
        builder.AppendLine("TBD");
        builder.AppendLine();
        builder.AppendLine("### Consequences");
        builder.AppendLine();
        builder.AppendLine("- Good, because …");
        builder.AppendLine("- Bad, because …");

        if (proposal.EvidenceFiles.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            builder.AppendLine();
            foreach (var evidenceFile in proposal.EvidenceFiles.Take(8))
            {
                builder.AppendLine($"- `{evidenceFile}`");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string NormalizeRelativePath(string value)
    {
        var segments = value
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length is 0)
        {
            throw new ArgumentException("ADR folder path is required.", nameof(value));
        }

        return string.Join('/', segments);
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

    private static string CreateUniqueSlug(string suggestedSlug, ISet<string> usedSlugs)
    {
        var normalizedSlug = NormalizeSlug(suggestedSlug);
        if (usedSlugs.Add(normalizedSlug))
        {
            return normalizedSlug;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalizedSlug}-{suffix}";
            if (usedSlugs.Add(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string NormalizeSlug(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var collapsed = SlugSanitizerRegex.Replace(lowered, "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "adr-bootstrap" : collapsed;
    }
}

/// <summary>
/// Represents bootstrap metadata for a managed repository.
/// </summary>
public sealed record AiBootstrapContext
{
    /// <summary>
    /// Gets the resolved repository metadata.
    /// </summary>
    public required ManagedRepository Repository { get; init; }

    /// <summary>
    /// Gets the count of ADR files currently in the repository.
    /// </summary>
    public required int ExistingAdrCount { get; init; }
}

/// <summary>
/// Represents results from accepting selected AI bootstrap proposals.
/// </summary>
public sealed record AiBootstrapAcceptResult
{
    /// <summary>
    /// Gets the ADRs created as proposed markdown documents.
    /// </summary>
    public required IReadOnlyList<Adr> CreatedAdrs { get; init; }

    /// <summary>
    /// Gets queue items emitted for later Git/PR automation.
    /// </summary>
    public required IReadOnlyList<GitPrWorkflowQueueItem> QueuedItems { get; init; }
}
