using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;
using System.Text.RegularExpressions;

namespace AdrPortal.Web.Services;

/// <summary>
/// Resolves ADR data for managed repositories.
/// </summary>
public sealed class AdrDocumentService(
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IGlobalAdrStore globalAdrStore,
    IGitPrWorkflowQueue? gitPrWorkflowQueue = null,
    IGitPrWorkflowProcessor? gitPrWorkflowProcessor = null)
{
    private const string DefaultBaseBranchName = "master";
    private static readonly Regex SlugRegex = new(
        @"^[a-z0-9][a-z0-9-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTagRegex = new(
        @"<\s*/?\s*[a-zA-Z][a-zA-Z0-9-]*(?=[\s/>])[^>]*>|<!--[\s\S]*?-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Gets the repository and ADR list for the specified repository identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>
    /// A tuple containing the resolved repository and ADR list, or <see langword="null"/> when the repository does not exist.
    /// </returns>
    public async Task<(ManagedRepository Repository, IReadOnlyList<Adr> Adrs)?> GetRepositoryWithAdrsAsync(int repositoryId, CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var adrs = await adrRepository.GetAllAsync(ct);
        return (repository, adrs);
    }

    /// <summary>
    /// Gets a single ADR with its repository context.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="number">ADR number.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>
    /// A tuple containing the repository and ADR when both are found; otherwise <see langword="null"/>.
    /// </returns>
    public async Task<(ManagedRepository Repository, Adr Adr)?> GetRepositoryAdrAsync(int repositoryId, int number, CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var adr = await adrRepository.GetByNumberAsync(number, ct);
        if (adr is null)
        {
            return null;
        }

        return (repository, adr);
    }

    /// <summary>
    /// Gets repository context and the next ADR number for create flows.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Repository context and next ADR number when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<(ManagedRepository Repository, int NextNumber)?> GetRepositoryForCreateAsync(int repositoryId, CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var nextNumber = await adrRepository.GetNextNumberAsync(ct);
        return (repository, nextNumber);
    }

    /// <summary>
    /// Creates and persists a new ADR markdown document.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="input">Normalized ADR editor input.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Persisted ADR result when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<AdrPersistResult?> CreateAdrAsync(int repositoryId, AdrEditorInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        ValidateInput(input);

        var nextNumber = await adrRepository.GetNextNumberAsync(ct);
        var adrToWrite = BuildAdrForWrite(repository, existingAdr: null, nextNumber, input);
        var persistedAdr = await adrRepository.WriteAsync(adrToWrite, ct);
        await QueueAndProcessAsync(
            repository,
            persistedAdr,
            GitPrWorkflowTrigger.RepositoryAdrPersist,
            GitPrWorkflowAction.CreateOrUpdatePullRequest,
            ct);

        return new AdrPersistResult
        {
            Repository = repository,
            Adr = persistedAdr
        };
    }

    /// <summary>
    /// Updates and persists an existing ADR markdown document.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="number">ADR number to update.</param>
    /// <param name="input">Normalized ADR editor input.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Persisted ADR result when repository and ADR exist; otherwise <see langword="null"/>.</returns>
    public async Task<AdrPersistResult?> UpdateAdrAsync(int repositoryId, int number, AdrEditorInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var existingAdr = await adrRepository.GetByNumberAsync(number, ct);
        if (existingAdr is null)
        {
            return null;
        }

        ValidateInput(input);

        var adrToWrite = BuildAdrForWrite(repository, existingAdr, number, input);
        var persistedAdr = await adrRepository.WriteAsync(adrToWrite, ct);
        await QueueAndProcessAsync(
            repository,
            persistedAdr,
            GitPrWorkflowTrigger.RepositoryAdrPersist,
            GitPrWorkflowAction.CreateOrUpdatePullRequest,
            ct);

        return new AdrPersistResult
        {
            Repository = repository,
            Adr = persistedAdr
        };
    }

    /// <summary>
    /// Applies a lifecycle transition action to an ADR from the detail route.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="number">ADR number.</param>
    /// <param name="action">Transition action to apply.</param>
    /// <param name="supersededByNumber">Optional superseding ADR number for supersede transitions.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>
    /// Transition result when repository and ADR are found; otherwise <see langword="null"/>.
    /// </returns>
    public async Task<AdrTransitionResult?> TransitionAdrAsync(
        int repositoryId,
        int number,
        AdrTransitionAction action,
        int? supersededByNumber,
        CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var existingAdr = await adrRepository.GetByNumberAsync(number, ct);
        if (existingAdr is null)
        {
            return null;
        }

        var transition = await BuildTransitionAsync(repository, existingAdr, action, supersededByNumber, ct);
        Adr persistedAdr;
        if (action is AdrTransitionAction.Reject)
        {
            await adrRepository.MoveToRejectedAsync(existingAdr.Number, ct);
            persistedAdr = await adrRepository.GetByNumberAsync(existingAdr.Number, ct)
                ?? throw new InvalidOperationException($"ADR-{existingAdr.Number:0000} could not be resolved after rejection.");
        }
        else
        {
            persistedAdr = await adrRepository.WriteAsync(transition.Adr, ct);
        }

        var workflowAction = action switch
        {
            AdrTransitionAction.Accept => GitPrWorkflowAction.MergePullRequest,
            AdrTransitionAction.Reject => GitPrWorkflowAction.ClosePullRequest,
            AdrTransitionAction.Supersede => GitPrWorkflowAction.CreateOrUpdatePullRequest,
            AdrTransitionAction.Deprecate => GitPrWorkflowAction.CreateOrUpdatePullRequest,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported transition action.")
        };
        await QueueAndProcessAsync(
            repository,
            persistedAdr,
            GitPrWorkflowTrigger.RepositoryAdrTransition,
            workflowAction,
            ct);

        return new AdrTransitionResult
        {
            Repository = repository,
            Adr = persistedAdr,
            Message = transition.Message
        };
    }

    /// <summary>
    /// Resolves repository metadata and a file-backed ADR repository for the requested identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Repository context and file repository when found; otherwise <see langword="null"/>.</returns>
    private async Task<(ManagedRepository Repository, IAdrFileRepository RepositoryFileStore)?> ResolveRepositoryAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = await madrRepositoryFactory.CreateAsync(repository, ct);
        return (repository, adrRepository);
    }

    /// <summary>
    /// Maps editor input and optional existing ADR metadata to a persisted ADR domain record.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <param name="existingAdr">Existing ADR record for edit flows.</param>
    /// <param name="number">ADR number to persist.</param>
    /// <param name="input">Validated editor input.</param>
    /// <returns>ADR domain record ready for repository persistence.</returns>
    private static Adr BuildAdrForWrite(
        ManagedRepository repository,
        Adr? existingAdr,
        int number,
        AdrEditorInput input)
    {
        var normalizedSlug = input.Slug.Trim().ToLowerInvariant();
        var normalizedTitle = input.Title.Trim();
        var normalizedBody = input.BodyMarkdown.Trim();
        var normalizedAdrFolder = repository.AdrFolder.Replace('\\', '/').Trim('/');
        var defaultPath = $"{normalizedAdrFolder}/adr-{number:0000}-{normalizedSlug}.md";
        var relativePath = existingAdr?.RepoRelativePath ?? defaultPath;

        return new Adr
        {
            Number = number,
            Slug = normalizedSlug,
            RepoRelativePath = relativePath,
            Title = normalizedTitle,
            Status = input.Status,
            Date = input.Date,
            GlobalId = existingAdr?.GlobalId,
            GlobalVersion = existingAdr?.GlobalVersion,
            DecisionMakers = input.DecisionMakers,
            Consulted = input.Consulted,
            Informed = input.Informed,
            SupersededByNumber = input.SupersededByNumber,
            RawMarkdown = normalizedBody.StartsWith("# ", StringComparison.Ordinal)
                ? normalizedBody
                : $"# {normalizedTitle}\n\n{normalizedBody}"
        };
    }

    /// <summary>
    /// Builds and validates ADR transition output including persistence side effects.
    /// </summary>
    /// <param name="repository">Repository containing the ADR.</param>
    /// <param name="existingAdr">Current ADR state.</param>
    /// <param name="action">Requested transition action.</param>
    /// <param name="supersededByNumber">Optional superseding ADR number.</param>
    /// <param name="ct">Cancellation token for async store operations.</param>
    /// <returns>Updated ADR and operation message.</returns>
    private async Task<(Adr Adr, string Message)> BuildTransitionAsync(
        ManagedRepository repository,
        Adr existingAdr,
        AdrTransitionAction action,
        int? supersededByNumber,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return action switch
        {
            AdrTransitionAction.Accept => await BuildAcceptTransitionAsync(repository, existingAdr, ct),
            AdrTransitionAction.Reject => BuildRejectTransition(existingAdr),
            AdrTransitionAction.Supersede => BuildSupersedeTransition(existingAdr, supersededByNumber),
            AdrTransitionAction.Deprecate => BuildDeprecateTransition(existingAdr),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported transition action.")
        };
    }

    /// <summary>
    /// Applies accept transition rules and global library registration behavior.
    /// </summary>
    /// <param name="repository">Repository containing the ADR.</param>
    /// <param name="existingAdr">Current ADR state.</param>
    /// <param name="ct">Cancellation token for async store operations.</param>
    /// <returns>Updated ADR and operation message.</returns>
    private async Task<(Adr Adr, string Message)> BuildAcceptTransitionAsync(
        ManagedRepository repository,
        Adr existingAdr,
        CancellationToken ct)
    {
        if (existingAdr.Status is not AdrStatus.Proposed)
        {
            throw new InvalidOperationException("Only proposed ADRs can be accepted.");
        }

        var nowUtc = DateTime.UtcNow;
        if (existingAdr.GlobalId is null)
        {
            var newGlobalId = Guid.NewGuid();
            var newGlobalAdr = new GlobalAdr
            {
                GlobalId = newGlobalId,
                Title = existingAdr.Title,
                CurrentVersion = 1,
                RegisteredAtUtc = nowUtc,
                LastUpdatedAtUtc = nowUtc,
                Instances = [],
                UpdateProposals = [],
                Versions = []
            };

            _ = await globalAdrStore.AddAsync(newGlobalAdr, ct);
            _ = await globalAdrStore.AddVersionAsync(
                new GlobalAdrVersion
                {
                    GlobalId = newGlobalId,
                    VersionNumber = 1,
                    Title = existingAdr.Title,
                    MarkdownContent = existingAdr.RawMarkdown,
                    CreatedAtUtc = nowUtc
                },
                ct);
            _ = await globalAdrStore.UpsertInstanceAsync(
                new GlobalAdrInstance
                {
                    GlobalId = newGlobalId,
                    RepositoryId = repository.Id,
                    LocalAdrNumber = existingAdr.Number,
                    RepoRelativePath = existingAdr.RepoRelativePath,
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 1,
                    HasLocalChanges = false,
                    UpdateAvailable = false,
                    LastReviewedAtUtc = nowUtc
                },
                ct);

            return (
                existingAdr with
                {
                    Status = AdrStatus.Accepted,
                    GlobalId = newGlobalId,
                    GlobalVersion = 1,
                    SupersededByNumber = null
                },
                $"ADR-{existingAdr.Number:0000} accepted and registered in the global library as v1.");
        }

        var globalAdr = await globalAdrStore.GetByIdAsync(existingAdr.GlobalId.Value, ct);
        if (globalAdr is null)
        {
            throw new InvalidOperationException(
                $"Global ADR '{existingAdr.GlobalId}' is not registered. Re-link before accepting this ADR.");
        }

        var baseVersion = existingAdr.GlobalVersion ?? globalAdr.CurrentVersion;
        if (baseVersion < 1)
        {
            throw new InvalidOperationException("Global ADR version must be greater than zero.");
        }

        _ = await globalAdrStore.UpsertInstanceAsync(
            new GlobalAdrInstance
            {
                GlobalId = existingAdr.GlobalId.Value,
                RepositoryId = repository.Id,
                LocalAdrNumber = existingAdr.Number,
                RepoRelativePath = existingAdr.RepoRelativePath,
                LastKnownStatus = AdrStatus.Accepted,
                BaseTemplateVersion = baseVersion,
                HasLocalChanges = false,
                UpdateAvailable = globalAdr.CurrentVersion > baseVersion,
                LastReviewedAtUtc = nowUtc
            },
            ct);

        return (
            existingAdr with
            {
                Status = AdrStatus.Accepted,
                GlobalVersion = baseVersion,
                SupersededByNumber = null
            },
            $"ADR-{existingAdr.Number:0000} accepted and linked to existing global ADR {existingAdr.GlobalId}.");
    }

    /// <summary>
    /// Applies reject transition rules.
    /// </summary>
    /// <param name="existingAdr">Current ADR state.</param>
    /// <returns>Updated ADR and operation message.</returns>
    private static (Adr Adr, string Message) BuildRejectTransition(Adr existingAdr)
    {
        if (existingAdr.Status is not AdrStatus.Proposed)
        {
            throw new InvalidOperationException("Only proposed ADRs can be rejected.");
        }

        return (
            existingAdr with
            {
                Status = AdrStatus.Rejected,
                SupersededByNumber = null
            },
            $"ADR-{existingAdr.Number:0000} rejected.");
    }

    /// <summary>
    /// Applies supersede transition rules.
    /// </summary>
    /// <param name="existingAdr">Current ADR state.</param>
    /// <param name="supersededByNumber">Superseding ADR number.</param>
    /// <returns>Updated ADR and operation message.</returns>
    private static (Adr Adr, string Message) BuildSupersedeTransition(Adr existingAdr, int? supersededByNumber)
    {
        if (existingAdr.Status is not AdrStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted ADRs can be superseded.");
        }

        if (supersededByNumber is null or <= 0)
        {
            throw new ArgumentException("Superseded-by ADR number must be greater than zero.", nameof(supersededByNumber));
        }

        if (supersededByNumber.Value == existingAdr.Number)
        {
            throw new ArgumentException("Superseded-by ADR number must be different from the current ADR.", nameof(supersededByNumber));
        }

        return (
            existingAdr with
            {
                Status = AdrStatus.Superseded,
                SupersededByNumber = supersededByNumber.Value
            },
            $"ADR-{existingAdr.Number:0000} marked as superseded by ADR-{supersededByNumber:0000}.");
    }

    /// <summary>
    /// Applies deprecate transition rules.
    /// </summary>
    /// <param name="existingAdr">Current ADR state.</param>
    /// <returns>Updated ADR and operation message.</returns>
    private static (Adr Adr, string Message) BuildDeprecateTransition(Adr existingAdr)
    {
        if (existingAdr.Status is not AdrStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted ADRs can be deprecated.");
        }

        return (
            existingAdr with
            {
                Status = AdrStatus.Deprecated,
                SupersededByNumber = null
            },
            $"ADR-{existingAdr.Number:0000} marked as deprecated.");
    }

    /// <summary>
    /// Validates editor input before creating or updating markdown files.
    /// </summary>
    /// <param name="input">Editor input to validate.</param>
    private static void ValidateInput(AdrEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new ArgumentException("Title is required.", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Slug))
        {
            throw new ArgumentException("Slug is required.", nameof(input));
        }

        if (!SlugRegex.IsMatch(input.Slug))
        {
            throw new ArgumentException("Slug must contain lowercase letters, numbers, and hyphens only.", nameof(input));
        }

        if (input.DecisionMakers.Count is 0)
        {
            throw new ArgumentException("At least one decision maker is required.", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.BodyMarkdown))
        {
            throw new ArgumentException("Markdown content is required.", nameof(input));
        }

        if (input.SupersededByNumber is <= 0)
        {
            throw new ArgumentException("Superseded-by ADR number must be greater than zero.", nameof(input));
        }

        EnsureTextContainsNoHtml(input.Title, "Title");
        EnsureTextContainsNoHtml(input.BodyMarkdown, "Markdown body");
        EnsureTextContainsNoHtml(string.Join('\n', input.DecisionMakers), "Decision makers");
        EnsureTextContainsNoHtml(string.Join('\n', input.Consulted), "Consulted");
        EnsureTextContainsNoHtml(string.Join('\n', input.Informed), "Informed");
    }

    /// <summary>
    /// Ensures a free-form text field does not include raw HTML markup.
    /// </summary>
    /// <param name="value">Text value to validate.</param>
    /// <param name="fieldName">Field display name for error messages.</param>
    private static void EnsureTextContainsNoHtml(string value, string fieldName)
    {
        if (!HtmlTagRegex.IsMatch(value))
        {
            return;
        }

        throw new ArgumentException($"{fieldName} cannot contain raw HTML markup.");
    }

    private async Task QueueAndProcessAsync(
        ManagedRepository repository,
        Adr adr,
        GitPrWorkflowTrigger trigger,
        GitPrWorkflowAction action,
        CancellationToken ct)
    {
        if (gitPrWorkflowQueue is null)
        {
            return;
        }

        var queueItem = await BuildQueueItemAsync(repository, adr, trigger, action, ct);
        await gitPrWorkflowQueue.EnqueueAsync(queueItem, ct);
        if (gitPrWorkflowProcessor is not null)
        {
            _ = await gitPrWorkflowProcessor.ProcessAndUpdateQueueAsync(queueItem.Id, ct);
        }
    }

    private async Task<GitPrWorkflowQueueItem> BuildQueueItemAsync(
        ManagedRepository repository,
        Adr adr,
        GitPrWorkflowTrigger trigger,
        GitPrWorkflowAction action,
        CancellationToken ct)
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
            Trigger = trigger,
            Action = action,
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
