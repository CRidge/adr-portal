using AdrPortal.Core.Madr;
using AdrPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdrPortal.Infrastructure.Extensions;

/// <summary>
/// Registers infrastructure persistence services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string AdrPortalConnectionStringName = "AdrPortal";

    /// <summary>
    /// Adds EF Core persistence services for ADR Portal.
    /// </summary>
    /// <param name="services">Dependency injection service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the ADR portal connection string is missing.</exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AdrPortalConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{AdrPortalConnectionStringName}' is required for infrastructure services.");
        }

        services.AddDbContext<AdrPortalDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddSingleton<IMadrParser, MadrParser>();
        services.AddSingleton<IMadrWriter, MadrWriter>();

        return services;
    }
}
