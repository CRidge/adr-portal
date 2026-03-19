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
    }
}
