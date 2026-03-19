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
    /// Gets all global ADR rows ordered by title and identifier.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>All global ADR rows.</returns>
    public async Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await dbContext.GlobalAdrs
            .AsNoTracking()
            .OrderBy(globalAdr => globalAdr.Title)
            .ThenBy(globalAdr => globalAdr.GlobalId)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Gets version rows for a global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Ordered version rows.</returns>
    public async Task<IReadOnlyList<GlobalAdrVersion>> GetVersionsAsync(Guid globalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await dbContext.GlobalAdrVersions
            .AsNoTracking()
            .Where(version => version.GlobalId == globalId)
            .OrderByDescending(version => version.VersionNumber)
            .ThenByDescending(version => version.Id)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Gets update proposal rows for a global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Proposal rows.</returns>
    public async Task<IReadOnlyList<GlobalAdrUpdateProposal>> GetUpdateProposalsAsync(Guid globalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await dbContext.GlobalAdrUpdateProposals
            .AsNoTracking()
            .Where(proposal => proposal.GlobalId == globalId)
            .OrderByDescending(proposal => proposal.IsPending)
            .ThenByDescending(proposal => proposal.CreatedAtUtc)
            .ThenByDescending(proposal => proposal.Id)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Gets instance rows for a global ADR.
    /// </summary>
    /// <param name="globalId">Global ADR identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Instance rows.</returns>
    public async Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return await dbContext.GlobalAdrInstances
            .AsNoTracking()
            .Where(instance => instance.GlobalId == globalId)
            .OrderBy(instance => instance.RepositoryId)
            .ThenBy(instance => instance.LocalAdrNumber)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Gets the combined dashboard pending count.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Total pending count across proposals and update-available instances.</returns>
    public async Task<int> GetDashboardPendingCountAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var pendingProposalCount = await dbContext.GlobalAdrUpdateProposals
            .AsNoTracking()
            .CountAsync(proposal => proposal.IsPending, ct);
        var updateAvailableCount = await dbContext.GlobalAdrInstances
            .AsNoTracking()
            .CountAsync(instance => instance.UpdateAvailable, ct);

        return pendingProposalCount + updateAvailableCount;
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
    /// Updates an existing global ADR row.
    /// </summary>
    /// <param name="globalAdr">Updated global ADR values.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The updated row when found; otherwise <see langword="null"/>.</returns>
    public async Task<GlobalAdr?> UpdateAsync(GlobalAdr globalAdr, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(globalAdr);
        ct.ThrowIfCancellationRequested();

        var existing = await dbContext.GlobalAdrs
            .SingleOrDefaultAsync(candidate => candidate.GlobalId == globalAdr.GlobalId, ct);

        if (existing is null)
        {
            return null;
        }

        existing.Title = globalAdr.Title;
        existing.CurrentVersion = globalAdr.CurrentVersion;
        existing.LastUpdatedAtUtc = globalAdr.LastUpdatedAtUtc;

        _ = await dbContext.SaveChangesAsync(ct);
        return existing;
    }

    /// <summary>
    /// Adds an immutable global ADR version row.
    /// </summary>
    /// <param name="version">Version row to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted version row.</returns>
    public async Task<GlobalAdrVersion> AddVersionAsync(GlobalAdrVersion version, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        ct.ThrowIfCancellationRequested();

        _ = await dbContext.GlobalAdrVersions.AddAsync(version, ct);
        _ = await dbContext.SaveChangesAsync(ct);
        return version;
    }

    /// <summary>
    /// Adds a global ADR update proposal row.
    /// </summary>
    /// <param name="proposal">Proposal row to persist.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The persisted proposal row.</returns>
    public async Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ct.ThrowIfCancellationRequested();

        _ = await dbContext.GlobalAdrUpdateProposals.AddAsync(proposal, ct);
        _ = await dbContext.SaveChangesAsync(ct);
        return proposal;
    }

    /// <summary>
    /// Updates a persisted update proposal row.
    /// </summary>
    /// <param name="proposal">Updated proposal values.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The updated proposal row when found; otherwise <see langword="null"/>.</returns>
    public async Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ct.ThrowIfCancellationRequested();

        var existing = await dbContext.GlobalAdrUpdateProposals
            .SingleOrDefaultAsync(candidate => candidate.Id == proposal.Id, ct);
        if (existing is null)
        {
            return null;
        }

        existing.ProposedFromVersion = proposal.ProposedFromVersion;
        existing.ProposedTitle = proposal.ProposedTitle;
        existing.ProposedMarkdownContent = proposal.ProposedMarkdownContent;
        existing.IsPending = proposal.IsPending;
        existing.CreatedAtUtc = proposal.CreatedAtUtc;

        _ = await dbContext.SaveChangesAsync(ct);
        return existing;
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
        existing.HasLocalChanges = instance.HasLocalChanges;
        existing.UpdateAvailable = instance.UpdateAvailable;
        existing.LastReviewedAtUtc = instance.LastReviewedAtUtc;

        _ = await dbContext.SaveChangesAsync(ct);
        return existing;
    }
}
