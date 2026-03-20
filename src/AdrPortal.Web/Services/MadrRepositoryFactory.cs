using System.Collections.Concurrent;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;
using LibGit2Sharp;
using AdrPortal.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace AdrPortal.Web.Services;

/// <summary>
/// Creates ADR file repositories using managed repository settings.
/// </summary>
public sealed class MadrRepositoryFactory(
    IMadrParser madrParser,
    IMadrWriter madrWriter,
    ILogger<MadrRepositoryFactory>? logger = null) : IMadrRepositoryFactory
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CheckoutLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a file-backed ADR repository.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <returns>A configured ADR file repository instance.</returns>
    public IAdrFileRepository Create(ManagedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var checkoutPath = EnsureCheckoutReady(repository);
        return new AdrFileRepository(
            repositoryRootPath: checkoutPath,
            adrFolderRelativePath: ManagedRepositoryDefaults.DefaultAdrFolder,
            madrParser: madrParser,
            madrWriter: madrWriter);
    }

    /// <summary>
    /// Ensures the managed checkout exists and is synchronized with the configured remote URL when provided.
    /// </summary>
    /// <param name="repository">Managed repository metadata.</param>
    /// <returns>Normalized absolute checkout path.</returns>
    private string EnsureCheckoutReady(ManagedRepository repository)
    {
        if (string.IsNullOrWhiteSpace(repository.RootPath))
        {
            throw new InvalidOperationException(
                $"Repository '{repository.DisplayName}' does not have a managed checkout path configured.");
        }

        var checkoutPath = Path.GetFullPath(repository.RootPath);
        var gitRemoteUrl = repository.GitRemoteUrl?.Trim();
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
        {
            Directory.CreateDirectory(checkoutPath);
            return checkoutPath;
        }

        var checkoutLock = CheckoutLocks.GetOrAdd(checkoutPath, static _ => new SemaphoreSlim(1, 1));
        checkoutLock.Wait();
        try
        {
            if (!Repository.IsValid(checkoutPath))
            {
                PrepareCloneTarget(checkoutPath);
                var cloneOptions = new CloneOptions
                {
                    Checkout = true
                };
                ConfigureFetchOptions(cloneOptions.FetchOptions, gitRemoteUrl);
                _ = Repository.Clone(
                    gitRemoteUrl,
                    checkoutPath,
                    cloneOptions);
                return checkoutPath;
            }

            using var gitRepository = new Repository(checkoutPath);
            var origin = EnsureOriginRemote(gitRepository, gitRemoteUrl);
            var fetchRefSpecs = origin.FetchRefSpecs.Select(spec => spec.Specification);
            var fetchOptions = new FetchOptions();
            ConfigureFetchOptions(fetchOptions, origin.Url);
            Commands.Fetch(gitRepository, origin.Name, fetchRefSpecs, fetchOptions, "ADR Portal managed checkout fetch");
            TryFastForwardLocalBranch(gitRepository);
            return checkoutPath;
        }
        catch (LibGit2SharpException exception)
        {
            throw new InvalidOperationException(
                $"Unable to prepare managed checkout for '{repository.DisplayName}' at '{checkoutPath}'.",
                exception);
        }
        finally
        {
            checkoutLock.Release();
        }
    }

    /// <summary>
    /// Ensures the clone target path is safe to initialize.
    /// </summary>
    /// <param name="checkoutPath">Managed checkout path.</param>
    private static void PrepareCloneTarget(string checkoutPath)
    {
        var parentDirectory = Path.GetDirectoryName(checkoutPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new InvalidOperationException($"Managed checkout path '{checkoutPath}' is invalid.");
        }

        Directory.CreateDirectory(parentDirectory);
        if (!Directory.Exists(checkoutPath))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(checkoutPath).Any())
        {
            throw new InvalidOperationException(
                $"Managed checkout path '{checkoutPath}' already exists and is not an empty Git repository.");
        }
    }

    /// <summary>
    /// Ensures an origin remote exists and matches the configured URL.
    /// </summary>
    /// <param name="repository">Git repository handle.</param>
    /// <param name="gitRemoteUrl">Configured remote URL.</param>
    /// <returns>The resolved origin remote.</returns>
    private static Remote EnsureOriginRemote(Repository repository, string gitRemoteUrl)
    {
        var origin = repository.Network.Remotes["origin"];
        if (origin is null)
        {
            return repository.Network.Remotes.Add("origin", gitRemoteUrl);
        }

        if (!string.Equals(origin.Url, gitRemoteUrl, StringComparison.Ordinal))
        {
            repository.Network.Remotes.Update("origin", updater =>
            {
                updater.Url = gitRemoteUrl;
            });
        }

        return repository.Network.Remotes["origin"]
            ?? throw new InvalidOperationException("Managed checkout must define an origin remote.");
    }

    /// <summary>
    /// Fast-forwards the current local branch to its tracked upstream branch when safe.
    /// </summary>
    /// <param name="repository">Git repository handle.</param>
    private void TryFastForwardLocalBranch(Repository repository)
    {
        if (repository.Info.IsHeadDetached)
        {
            return;
        }

        var trackedBranch = repository.Head.TrackedBranch;
        if (trackedBranch?.Tip is null || repository.Head.Tip is null)
        {
            return;
        }

        if (repository.RetrieveStatus().IsDirty)
        {
            logger?.LogWarning(
                "Skipping managed checkout fast-forward for '{RepositoryPath}' because the working tree has local changes.",
                repository.Info.WorkingDirectory);
            return;
        }

        var divergence = repository.ObjectDatabase.CalculateHistoryDivergence(repository.Head.Tip, trackedBranch.Tip);
        var behindBy = divergence?.BehindBy ?? 0;
        var aheadBy = divergence?.AheadBy ?? 0;
        if (behindBy > 0 && aheadBy is 0)
        {
            repository.Reset(ResetMode.Hard, trackedBranch.Tip);
        }
    }

    /// <summary>
    /// Builds fetch options with optional GitHub token authentication for private remotes.
    /// </summary>
    /// <returns>Fetch options for clone and fetch operations.</returns>
    private static void ConfigureFetchOptions(FetchOptions fetchOptions, string? gitRemoteUrl)
    {
        ArgumentNullException.ThrowIfNull(fetchOptions);
        if (!ShouldUseGitHubToken(gitRemoteUrl))
        {
            return;
        }

        var gitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(gitHubToken))
        {
            return;
        }

        fetchOptions.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
        {
            Username = "x-access-token",
            Password = gitHubToken.Trim()
        };
    }

    /// <summary>
    /// Determines whether GitHub token authentication should be applied for the remote URL.
    /// </summary>
    /// <param name="gitRemoteUrl">Remote URL used for clone/fetch.</param>
    /// <returns><see langword="true"/> when the remote targets github.com over HTTPS.</returns>
    private static bool ShouldUseGitHubToken(string? gitRemoteUrl)
    {
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(gitRemoteUrl.Trim(), UriKind.Absolute, out var remoteUri))
        {
            return false;
        }

        return string.Equals(remoteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(remoteUri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
    }
}
