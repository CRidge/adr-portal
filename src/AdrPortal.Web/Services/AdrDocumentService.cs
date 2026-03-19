using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Resolves ADR data for managed repositories.
/// </summary>
public sealed class AdrDocumentService(IManagedRepositoryStore managedRepositoryStore, IMadrRepositoryFactory madrRepositoryFactory)
{
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
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = madrRepositoryFactory.Create(repository);
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
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = madrRepositoryFactory.Create(repository);
        var adr = await adrRepository.GetByNumberAsync(number, ct);
        if (adr is null)
        {
            return null;
        }

        return (repository, adr);
    }
}
