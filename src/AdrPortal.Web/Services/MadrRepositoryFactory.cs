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
    IGitRepositoryService gitRepositoryService) : IMadrRepositoryFactory
{
    /// <summary>
    /// Ensures repository checkout state and creates a file-backed ADR repository.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <param name="ct">Cancellation token for checkout and repository initialization.</param>
    /// <returns>A configured ADR file repository instance.</returns>
    public async Task<IAdrFileRepository> CreateAsync(ManagedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ct.ThrowIfCancellationRequested();
        await gitRepositoryService.EnsureRepositoryReadyAsync(repository, ct);

        return new AdrFileRepository(
            repositoryRootPath: repository.RootPath,
            adrFolderRelativePath: repository.AdrFolder,
            madrParser: madrParser,
            madrWriter: madrWriter);
    }
}
