using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Web.State;

/// <summary>
/// Maintains the current managed repository list for interactive UI components.
/// </summary>
public sealed class RepositoryCatalogState(
    IManagedRepositoryStore managedRepositoryStore,
    IGlobalAdrStore globalAdrStore)
{
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    /// <summary>
    /// Occurs when the repository list changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets the latest repository list.
    /// </summary>
    public IReadOnlyList<ManagedRepository> Repositories { get; private set; } = [];

    /// <summary>
    /// Gets the dashboard badge count for pending global sync work.
    /// </summary>
    public int DashboardPendingCount { get; private set; }

    /// <summary>
    /// Refreshes repositories from persistence and raises change notifications.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An asynchronous operation.</returns>
    public async Task RefreshAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await refreshGate.WaitAsync(ct);
        try
        {
            Repositories = await managedRepositoryStore.GetAllAsync(ct);
            DashboardPendingCount = await globalAdrStore.GetDashboardPendingCountAsync(ct);
        }
        finally
        {
            refreshGate.Release();
        }

        Changed?.Invoke();
    }
}
