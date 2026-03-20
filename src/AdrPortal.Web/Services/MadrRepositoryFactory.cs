using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Creates ADR file repositories using managed repository settings.
/// </summary>
public sealed class MadrRepositoryFactory(
    IMadrParser madrParser,
    IMadrWriter madrWriter,
    IManagedRepositoryCheckoutService managedRepositoryCheckoutService) : IMadrRepositoryFactory
{
    /// <summary>
    /// Creates a file-backed ADR repository.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <param name="ct">Cancellation token for checkout synchronization.</param>
    /// <returns>A configured ADR file repository instance.</returns>
    public async Task<IAdrFileRepository> CreateAsync(ManagedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ct.ThrowIfCancellationRequested();

        var checkoutPath = await managedRepositoryCheckoutService.EnsureLocalCheckoutAsync(repository, ct);

        return new AdrFileRepository(
            repositoryRootPath: checkoutPath,
            adrFolderRelativePath: repository.AdrFolder,
            madrParser: madrParser,
            madrWriter: madrWriter);
    }
}
