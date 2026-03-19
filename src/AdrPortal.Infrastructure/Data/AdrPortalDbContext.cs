using AdrPortal.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdrPortal.Infrastructure.Data;

/// <summary>
/// EF Core database context for ADR Portal data.
/// </summary>
public class AdrPortalDbContext(DbContextOptions<AdrPortalDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets managed repositories configured in the portal.
    /// </summary>
    public DbSet<ManagedRepository> ManagedRepositories => Set<ManagedRepository>();

    /// <summary>
    /// Gets or sets globally registered ADR topics.
    /// </summary>
    public DbSet<GlobalAdr> GlobalAdrs => Set<GlobalAdr>();

    /// <summary>
    /// Gets or sets repository ADR instances linked to global ADR topics.
    /// </summary>
    public DbSet<GlobalAdrInstance> GlobalAdrInstances => Set<GlobalAdrInstance>();

    /// <summary>
    /// Gets or sets immutable version snapshots for global ADR topics.
    /// </summary>
    public DbSet<GlobalAdrVersion> GlobalAdrVersions => Set<GlobalAdrVersion>();

    /// <summary>
    /// Gets or sets repository-to-library update proposals for global ADR topics.
    /// </summary>
    public DbSet<GlobalAdrUpdateProposal> GlobalAdrUpdateProposals => Set<GlobalAdrUpdateProposal>();

    /// <summary>
    /// Configures entity mappings for the portal data model.
    /// </summary>
    /// <param name="modelBuilder">Model builder used to configure EF Core mappings.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManagedRepository>(entity =>
        {
            entity.ToTable("ManagedRepositories");
            entity.HasKey(repository => repository.Id);

            entity.Property(repository => repository.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(repository => repository.RootPath)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(repository => repository.AdrFolder)
                .HasMaxLength(260)
                .HasDefaultValue("docs/adr")
                .IsRequired();

            entity.Property(repository => repository.InboxFolder)
                .HasMaxLength(1024);

            entity.Property(repository => repository.GitRemoteUrl)
                .HasMaxLength(2048);

            entity.Property(repository => repository.IsActive)
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(repository => repository.CreatedAtUtc)
                .IsRequired();

            entity.Property(repository => repository.UpdatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<GlobalAdr>(entity =>
        {
            entity.ToTable("GlobalAdrs");
            entity.HasKey(globalAdr => globalAdr.GlobalId);

            entity.Property(globalAdr => globalAdr.Title)
                .HasMaxLength(400)
                .IsRequired();

            entity.Property(globalAdr => globalAdr.CurrentVersion)
                .IsRequired();

            entity.Property(globalAdr => globalAdr.RegisteredAtUtc)
                .IsRequired();

            entity.Property(globalAdr => globalAdr.LastUpdatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<GlobalAdrInstance>(entity =>
        {
            entity.ToTable("GlobalAdrInstances");
            entity.HasKey(instance => instance.Id);

            entity.Property(instance => instance.RepoRelativePath)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(instance => instance.LastKnownStatus)
                .IsRequired();

            entity.Property(instance => instance.BaseTemplateVersion)
                .IsRequired();

            entity.Property(instance => instance.HasLocalChanges)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(instance => instance.UpdateAvailable)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(instance => instance.LastReviewedAtUtc)
                .IsRequired();

            entity.HasIndex(instance => new { instance.GlobalId, instance.RepositoryId, instance.LocalAdrNumber })
                .IsUnique();

            entity.HasOne(instance => instance.GlobalAdr)
                .WithMany(globalAdr => globalAdr.Instances)
                .HasForeignKey(instance => instance.GlobalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(instance => instance.Repository)
                .WithMany()
                .HasForeignKey(instance => instance.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GlobalAdrVersion>(entity =>
        {
            entity.ToTable("GlobalAdrVersions");
            entity.HasKey(version => version.Id);

            entity.Property(version => version.VersionNumber)
                .IsRequired();

            entity.Property(version => version.Title)
                .HasMaxLength(400)
                .IsRequired();

            entity.Property(version => version.MarkdownContent)
                .IsRequired();

            entity.Property(version => version.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(version => new { version.GlobalId, version.VersionNumber })
                .IsUnique();

            entity.HasOne(version => version.GlobalAdr)
                .WithMany(globalAdr => globalAdr.Versions)
                .HasForeignKey(version => version.GlobalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GlobalAdrUpdateProposal>(entity =>
        {
            entity.ToTable("GlobalAdrUpdateProposals");
            entity.HasKey(proposal => proposal.Id);

            entity.Property(proposal => proposal.ProposedFromVersion)
                .IsRequired();

            entity.Property(proposal => proposal.ProposedTitle)
                .HasMaxLength(400)
                .IsRequired();

            entity.Property(proposal => proposal.ProposedMarkdownContent)
                .IsRequired();

            entity.Property(proposal => proposal.IsPending)
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(proposal => proposal.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(proposal => new { proposal.GlobalId, proposal.RepositoryId, proposal.LocalAdrNumber, proposal.IsPending });

            entity.HasOne(proposal => proposal.GlobalAdr)
                .WithMany(globalAdr => globalAdr.UpdateProposals)
                .HasForeignKey(proposal => proposal.GlobalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(proposal => proposal.Repository)
                .WithMany()
                .HasForeignKey(proposal => proposal.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
