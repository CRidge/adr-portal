using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Repositories;

/// <summary>
/// Persists global ADR records and repository instance mappings in EF Core.
/// </summary>
public sealed class GlobalAdrStore(AdrPortalDbContext dbContext) : IGlobalAdrStore
{
    /// <summary>
    /// Gets a global ADR by identifier.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The global ADR when found; otherwise <see langword="null"/>.</returns>
    public async Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await dbContext.GlobalAdrs
            .AsNoTracking()
            .SingleOrDefaultAsync(globalAdr => globalAdr.GlobalId == globalId, ct);
    }

    /// <summary>
    /// Adds a new global ADR row.
    /// </summary>
    /// <param name="globalAdr">Global ADR values to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted global ADR row.</returns>
    public async Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(globalAdr);
        ct.ThrowIfCancellationRequested();

        _ = await dbContext.GlobalAdrs.AddAsync(globalAdr, ct);
        _ = await dbContext.SaveChangesAsync(ct);
        return globalAdr;
    }

    /// <summary>
    /// Creates or updates a global ADR repository instance mapping.
    /// </summary>
    /// <param name="instance">Instance mapping values.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted mapping row.</returns>
    public async Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ct.ThrowIfCancellationRequested();

        var existing = await dbContext.GlobalAdrInstances
            .SingleOrDefaultAsync(
                candidate => candidate.GlobalId == instance.GlobalId
                    && candidate.RepositoryId == instance.RepositoryId
                    && candidate.LocalAdrNumber == instance.LocalAdrNumber,
                ct);

        if (existing is null)
        {
            _ = await dbContext.GlobalAdrInstances.AddAsync(instance, ct);
            _ = await dbContext.SaveChangesAsync(ct);
            return instance;
        }

        existing.RepoRelativePath = instance.RepoRelativePath;
        existing.LastKnownStatus = instance.LastKnownStatus;
        existing.BaseTemplateVersion = instance.BaseTemplateVersion;
        existing.LastReviewedAtUtc = instance.LastReviewedAtUtc;

        _ = await dbContext.SaveChangesAsync(ct);
        return existing;
    }
}
