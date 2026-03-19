namespace AdrPortal.Web.Services;

/// <summary>
/// Coordinates file-system watchers for repository inbox folders.
/// </summary>
public interface IInboxWatcherCoordinator
{
    /// <summary>
    /// Refreshes watcher registrations to match current managed repositories.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    Task RefreshWatchersAsync(CancellationToken ct);
}
