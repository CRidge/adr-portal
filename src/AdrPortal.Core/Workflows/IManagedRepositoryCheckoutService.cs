using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Workflows;

/// <summary>
/// Ensures managed repositories are cloned and refreshed in app-managed local checkout paths.
/// </summary>
public interface IManagedRepositoryCheckoutService
{
    /// <summary>
    /// Ensures a repository checkout exists locally, cloning when the managed path is not yet initialized.
    /// </summary>
    /// <param name="repository">Managed repository configuration.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Absolute local checkout path.</returns>
    Task<string> EnsureLocalCheckoutAsync(ManagedRepository repository, CancellationToken ct);

    /// <summary>
    /// Refreshes a repository checkout by syncing from the configured remote.
    /// </summary>
    /// <param name="repository">Managed repository configuration.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Absolute local checkout path.</returns>
    Task<string> RefreshCheckoutAsync(ManagedRepository repository, CancellationToken ct);
}
