using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;
using AdrPortal.Infrastructure.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Creates ADR file repositories using managed repository settings.
/// </summary>
public sealed class MadrRepositoryFactory(IMadrParser madrParser, IMadrWriter madrWriter) : IMadrRepositoryFactory
{
    /// <summary>
    /// Creates a file-backed ADR repository.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <returns>A configured ADR file repository instance.</returns>
    public IAdrFileRepository Create(ManagedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return new AdrFileRepository(
            repositoryRootPath: repository.RootPath,
            adrFolderRelativePath: repository.AdrFolder,
            madrParser: madrParser,
            madrWriter: madrWriter);
    }
}
