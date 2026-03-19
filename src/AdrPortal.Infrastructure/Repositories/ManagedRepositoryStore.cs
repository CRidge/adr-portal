using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Repositories;

/// <summary>
/// Persists managed repository records in EF Core.
/// </summary>
public sealed class ManagedRepositoryStore(AdrPortalDbContext dbContext) : IManagedRepositoryStore
{
    /// <summary>
    /// Gets a managed repository by identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The managed repository when found; otherwise <see langword="null"/>.</returns>
    public async Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await dbContext.ManagedRepositories
            .AsNoTracking()
            .SingleOrDefaultAsync(repository => repository.Id == repositoryId, ct);
    }

    /// <summary>
    /// Gets all managed repositories ordered by display name and identifier.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Managed repositories from the data store.</returns>
    public async Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await dbContext.ManagedRepositories
            .AsNoTracking()
            .OrderBy(repository => repository.DisplayName)
            .ThenBy(repository => repository.Id)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Gets managed repositories for a specific identifier set.
    /// </summary>
    /// <param name="repositoryIds">Repository identifiers to load.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Dictionary of repositories by identifier.</returns>
    public async Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        ct.ThrowIfCancellationRequested();

        if (repositoryIds.Count is 0)
        {
            return new Dictionary<int, ManagedRepository>();
        }

        var repositoryIdSet = repositoryIds.Distinct().ToArray();
        var repositories = await dbContext.ManagedRepositories
            .AsNoTracking()
            .Where(repository => repositoryIdSet.Contains(repository.Id))
            .ToArrayAsync(ct);

        return repositories.ToDictionary(repository => repository.Id);
    }

    /// <summary>
    /// Adds a managed repository to persistence.
    /// </summary>
    /// <param name="repository">Repository values to store.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted repository, including generated identifier values.</returns>
    public async Task<ManagedRepository> AddAsync(ManagedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ct.ThrowIfCancellationRequested();

        var timestamp = DateTime.UtcNow;
        repository.CreatedAtUtc = timestamp;
        repository.UpdatedAtUtc = timestamp;

        _ = await dbContext.ManagedRepositories.AddAsync(repository, ct);
        _ = await dbContext.SaveChangesAsync(ct);
        return repository;
    }

    /// <summary>
    /// Updates a managed repository in persistence.
    /// </summary>
    /// <param name="repository">Repository values to update.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The updated repository when found; otherwise <see langword="null"/>.</returns>
    public async Task<ManagedRepository?> UpdateAsync(ManagedRepository repository, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ct.ThrowIfCancellationRequested();

        var existingRepository = await dbContext.ManagedRepositories
            .SingleOrDefaultAsync(candidate => candidate.Id == repository.Id, ct);

        if (existingRepository is null)
        {
            return null;
        }

        existingRepository.DisplayName = repository.DisplayName;
        existingRepository.RootPath = repository.RootPath;
        existingRepository.AdrFolder = repository.AdrFolder;
        existingRepository.InboxFolder = repository.InboxFolder;
        existingRepository.GitRemoteUrl = repository.GitRemoteUrl;
        existingRepository.IsActive = repository.IsActive;
        existingRepository.UpdatedAtUtc = DateTime.UtcNow;

        _ = await dbContext.SaveChangesAsync(ct);
        return existingRepository;
    }

    /// <summary>
    /// Deletes a managed repository from persistence.
    /// </summary>
    /// <param name="repositoryId">Identifier of the repository to delete.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns><see langword="true"/> when a repository was deleted; otherwise <see langword="false"/>.</returns>
    public async Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var existingRepository = await dbContext.ManagedRepositories
            .SingleOrDefaultAsync(candidate => candidate.Id == repositoryId, ct);

        if (existingRepository is null)
        {
            return false;
        }

        _ = dbContext.ManagedRepositories.Remove(existingRepository);
        _ = await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
