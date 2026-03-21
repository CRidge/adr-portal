using AdrPortal.Core.Ai;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Ai;
using AdrPortal.Infrastructure.Data;
using AdrPortal.Infrastructure.Repositories;
using AdrPortal.Infrastructure.Workflows;
using Microsoft.Extensions.AI;
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
    /// <param name="contentRootPath">Application content root used to locate legacy relative database files.</param>
    /// <returns>The same service collection instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the ADR portal connection string is missing.</exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRootPath = null)
    {
        var connectionString = configuration.GetConnectionString(AdrPortalConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{AdrPortalConnectionStringName}' is required for infrastructure services.");
        }

        var configuredDatabaseRootPath = configuration[
            $"{PersistenceOptions.SectionName}:{nameof(PersistenceOptions.DatabaseRootPath)}"];
        var resetDatabaseOnStartup = bool.TryParse(
            configuration[$"{PersistenceOptions.SectionName}:{nameof(PersistenceOptions.ResetDatabaseOnStartup)}"],
            out var parsedResetDatabaseOnStartup)
            && parsedResetDatabaseOnStartup;
        if (!string.IsNullOrWhiteSpace(configuredDatabaseRootPath))
        {
            configuredDatabaseRootPath = Environment.ExpandEnvironmentVariables(configuredDatabaseRootPath);
            if (!Path.IsPathRooted(configuredDatabaseRootPath))
            {
                var rootAnchor = string.IsNullOrWhiteSpace(contentRootPath)
                    ? AppContext.BaseDirectory
                    : contentRootPath;
                configuredDatabaseRootPath = Path.GetFullPath(Path.Combine(rootAnchor, configuredDatabaseRootPath));
            }
        }

        services.Configure<PersistenceOptions>(options =>
        {
            options.DatabaseRootPath = configuredDatabaseRootPath;
            options.ResetDatabaseOnStartup = resetDatabaseOnStartup;
        });

        var resolvedConnectionString = SqliteConnectionStringResolver.ResolveConnectionString(
            connectionString,
            configuredDatabaseRootPath,
            contentRootPath,
            Environment.CurrentDirectory,
            AppContext.BaseDirectory);

        services.AddDbContext<AdrPortalDbContext>(options =>
        {
            options.UseSqlite(resolvedConnectionString);
        });

        services.AddScoped<IManagedRepositoryStore, ManagedRepositoryStore>();
        services.AddScoped<IGlobalAdrStore, GlobalAdrStore>();
        services.AddSingleton<IMadrParser, MadrParser>();
        services.AddSingleton<IMadrWriter, MadrWriter>();
        var aiConfiguration = configuration.GetSection(AiProviderOptions.SectionName);
        services.Configure<AiProviderOptions>(options =>
        {
            options.Provider = aiConfiguration["Provider"] ?? options.Provider;
            options.Model = aiConfiguration["Model"] ?? options.Model;
            options.Endpoint = aiConfiguration["Endpoint"] ?? options.Endpoint;
            if (bool.TryParse(aiConfiguration["AllowDeterministicFallback"], out var parsedAllowFallback))
            {
                options.AllowDeterministicFallback = parsedAllowFallback;
            }
        });
        services.AddSingleton<DeterministicAiService>();
        RegisterAiProviderServices(services, configuration);
        var gitHubConfiguration = configuration.GetSection(GitHubOptions.SectionName);
        services.Configure<GitHubOptions>(options =>
        {
            options.DefaultBaseBranch = gitHubConfiguration["DefaultBaseBranch"] ?? options.DefaultBaseBranch;
            options.CommitAuthorName = gitHubConfiguration["CommitAuthorName"] ?? options.CommitAuthorName;
            options.CommitAuthorEmail = gitHubConfiguration["CommitAuthorEmail"] ?? options.CommitAuthorEmail;
            options.TokenUserName = gitHubConfiguration["TokenUserName"] ?? options.TokenUserName;
        });
        services.AddSingleton<IGitPrWorkflowQueue, InMemoryGitPrWorkflowQueue>();
        services.AddSingleton<IGitCredentialResolver, EnvironmentGitCredentialResolver>();
        services.AddSingleton<IGitRepositoryService, LibGit2SharpRepositoryService>();
        services.AddSingleton<IGitHubPullRequestService, OctokitGitHubPullRequestService>();
        services.AddSingleton<IGitPrWorkflowProcessor, GitPrWorkflowProcessor>();

        return services;
    }

    private static void RegisterAiProviderServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IAiService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AiProviderOptions>>().Value;
            if (!options.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                && !options.Provider.Equals("Copilot", StringComparison.OrdinalIgnoreCase))
            {
                return serviceProvider.GetRequiredService<DeterministicAiService>();
            }

            var token = AiProviderTokenResolver.ResolveToken(options.Provider, configuration);
            var tokenValue = token ?? string.Empty;
            var missingTokenReason = string.IsNullOrWhiteSpace(token)
                ? "External AI provider is configured but COPILOT_TOKEN is missing. Deterministic fallback used."
                : string.Empty;

            return ActivatorUtilities.CreateInstance<ExternalAiService>(
                serviceProvider,
                tokenValue,
                missingTokenReason);
        });
    }
}
