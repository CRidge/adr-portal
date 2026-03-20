using AdrPortal.Core.Entities;
using AdrPortal.Core.Workflows;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Manages deterministic clone and refresh operations for managed repository working copies.
/// </summary>
public sealed class LibGit2SharpManagedRepositoryCheckoutService(ILogger<LibGit2SharpManagedRepositoryCheckoutService> logger) : IManagedRepositoryCheckoutService
{
    /// <inheritdoc />
    public Task<string> EnsureLocalCheckoutAsync(ManagedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ct.ThrowIfCancellationRequested();

        var checkoutPath = Path.GetFullPath(repository.RootPath);
        if (Repository.IsValid(checkoutPath))
        {
            return Task.FromResult(checkoutPath);
        }

        if (Directory.Exists(checkoutPath) && Directory.EnumerateFileSystemEntries(checkoutPath).Any())
        {
            throw new InvalidOperationException(
                $"Managed checkout path '{checkoutPath}' exists but is not a git repository. Remove the folder and re-run repository sync.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(checkoutPath) ?? checkoutPath);
        logger.LogInformation(
            "Cloning repository '{RepositoryDisplayName}' from '{RemoteUrl}' into '{CheckoutPath}'.",
            repository.DisplayName,
            repository.GitRemoteUrl,
            checkoutPath);

        try
        {
            _ = Repository.Clone(
                sourceUrl: ResolveRemote(repository),
                workdirPath: checkoutPath,
                options: new CloneOptions());
        }
        catch (LibGit2SharpException exception)
        {
            throw new InvalidOperationException(
                $"Failed to clone '{repository.GitRemoteUrl}' into '{checkoutPath}': {exception.Message}",
                exception);
        }

        return Task.FromResult(checkoutPath);
    }

    /// <inheritdoc />
    public async Task<string> RefreshCheckoutAsync(ManagedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ct.ThrowIfCancellationRequested();

        var checkoutPath = await EnsureLocalCheckoutAsync(repository, ct);
        logger.LogInformation(
            "Refreshing repository '{RepositoryDisplayName}' from '{RemoteUrl}' in '{CheckoutPath}'.",
            repository.DisplayName,
            repository.GitRemoteUrl,
            checkoutPath);

        try
        {
            using var gitRepository = new Repository(checkoutPath);
            var remote = ResolveOriginRemote(gitRepository, checkoutPath);
            Commands.Fetch(
                gitRepository,
                remote.Name,
                remote.FetchRefSpecs.Select(spec => spec.Specification),
                options: null,
                logMessage: null);
            SyncTrackedBranch(gitRepository, checkoutPath);
        }
        catch (LibGit2SharpException exception)
        {
            throw new InvalidOperationException(
                $"Failed to refresh managed checkout at '{checkoutPath}': {exception.Message}",
                exception);
        }

        return checkoutPath;
    }

    private static string ResolveRemote(ManagedRepository repository)
    {
        if (string.IsNullOrWhiteSpace(repository.GitRemoteUrl))
        {
            throw new InvalidOperationException("Git remote URL is required for managed checkout operations.");
        }

        return repository.GitRemoteUrl.Trim();
    }

    private static void SyncTrackedBranch(Repository repository, string checkoutPath)
    {
        if (repository.Info.IsHeadDetached)
        {
            return;
        }

        var head = repository.Head;
        var trackedBranch = head.TrackedBranch;
        if (trackedBranch is null || trackedBranch.Tip is null || head.Tip is null)
        {
            return;
        }

        var divergence = repository.ObjectDatabase.CalculateHistoryDivergence(head.Tip, trackedBranch.Tip);
        if (divergence is null || divergence.BehindBy is null or 0)
        {
            return;
        }

        if (repository.RetrieveStatus().IsDirty)
        {
            throw new InvalidOperationException(
                $"Managed checkout '{checkoutPath}' has local changes and cannot be auto-refreshed. Commit or discard local changes, then retry.");
        }

        if (divergence.AheadBy is > 0)
        {
            throw new InvalidOperationException(
                $"Managed checkout '{checkoutPath}' has local commits that diverge from tracked remote '{trackedBranch.FriendlyName}'.");
        }

        repository.Reset(ResetMode.Hard, trackedBranch.Tip);
    }

    private static Remote ResolveOriginRemote(Repository repository, string checkoutPath)
    {
        return repository.Network.Remotes["origin"]
            ?? throw new InvalidOperationException(
                $"Managed checkout '{checkoutPath}' does not define an 'origin' remote.");
    }
}
