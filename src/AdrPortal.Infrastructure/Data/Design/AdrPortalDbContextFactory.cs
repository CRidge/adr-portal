using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdrPortal.Infrastructure.Data.Design;

/// <summary>
/// Design-time factory for generating EF Core migrations.
/// </summary>
public sealed class AdrPortalDbContextFactory : IDesignTimeDbContextFactory<AdrPortalDbContext>
{
    /// <summary>
    /// Creates a design-time <see cref="AdrPortalDbContext"/> instance.
    /// </summary>
    /// <param name="args">Design-time arguments passed by tooling.</param>
    /// <returns>A configured database context instance.</returns>
    public AdrPortalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdrPortalDbContext>();
        optionsBuilder.UseSqlite("Data Source=adrportal.design.db");

        return new AdrPortalDbContext(optionsBuilder.Options);
    }
}
