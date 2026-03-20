using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Web.Services;

/// <summary>
/// Creates file-backed ADR repositories for managed repositories.
/// </summary>
public interface IMadrRepositoryFactory
{
    /// <summary>
    /// Ensures repository checkout state and creates a file-backed ADR repository for the provided managed repository.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <param name="ct">Cancellation token for checkout and repository initialization.</param>
    /// <returns>A configured file-backed ADR repository.</returns>
    Task<IAdrFileRepository> CreateAsync(ManagedRepository repository, CancellationToken ct);
}
