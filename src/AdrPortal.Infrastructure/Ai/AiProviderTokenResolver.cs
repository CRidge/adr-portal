using Microsoft.Extensions.Configuration;

namespace AdrPortal.Infrastructure.Ai;

/// <summary>
/// Resolves AI provider credentials from environment and configuration.
/// </summary>
public static class AiProviderTokenResolver
{
    /// <summary>
    /// Resolves the configured token for the selected AI provider.
    /// </summary>
    /// <param name="provider">Configured provider identifier.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Resolved token when available; otherwise <see langword="null"/>.</returns>
    public static string? ResolveToken(string provider, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var normalizedProvider = provider?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProvider))
        {
            return null;
        }

        if (normalizedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            || normalizedProvider.Equals("Copilot", StringComparison.OrdinalIgnoreCase))
        {
            var token = Environment.GetEnvironmentVariable("COPILOT_TOKEN")
                ?? configuration["AI:COPILOT_TOKEN"]
                ?? configuration["COPILOT_TOKEN"];
            return NormalizeToken(token);
        }

        return null;
    }

    private static string? NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
