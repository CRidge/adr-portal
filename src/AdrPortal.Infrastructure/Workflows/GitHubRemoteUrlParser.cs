using System.Text.RegularExpressions;
using AdrPortal.Core.Workflows;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Parses GitHub remote URLs into owner and repository segments.
/// </summary>
internal static partial class GitHubRemoteUrlParser
{
    /// <summary>
    /// Parses a Git remote URL and extracts GitHub owner/repository values.
    /// </summary>
    /// <param name="gitRemoteUrl">Git remote URL from repository configuration.</param>
    /// <returns>Parsed owner and repository reference.</returns>
    /// <exception cref="GitPrWorkflowException">Thrown when the URL is missing, unsupported, or malformed.</exception>
    internal static GitHubRepositoryReference Parse(string gitRemoteUrl)
    {
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
        {
            throw new GitPrWorkflowException("Git remote URL is required for GitHub PR integration.");
        }

        var normalizedUrl = gitRemoteUrl.Trim();
        if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (!string.Equals(absoluteUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new GitPrWorkflowException(
                    $"Git remote '{normalizedUrl}' is not a GitHub host. Only github.com remotes are supported in phase 11.");
            }

            var segments = absoluteUri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(TrimGitSuffix)
                .ToArray();
            if (segments.Length < 2 || string.IsNullOrWhiteSpace(segments[^2]) || string.IsNullOrWhiteSpace(segments[^1]))
            {
                throw new GitPrWorkflowException(
                    $"Git remote '{normalizedUrl}' must include owner and repository segments.");
            }

            return new GitHubRepositoryReference(segments[^2], segments[^1]);
        }

        var scpMatch = GitHubScpPattern().Match(normalizedUrl);
        if (!scpMatch.Success)
        {
            throw new GitPrWorkflowException(
                $"Git remote '{normalizedUrl}' must be a supported GitHub HTTPS or SSH URL.");
        }

        var owner = scpMatch.Groups["owner"].Value;
        var repository = TrimGitSuffix(scpMatch.Groups["repo"].Value);
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
        {
            throw new GitPrWorkflowException(
                $"Git remote '{normalizedUrl}' must include owner and repository segments.");
        }

        return new GitHubRepositoryReference(owner, repository);
    }

    private static string TrimGitSuffix(string value)
    {
        return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }

    [GeneratedRegex("^git@github\\.com:(?<owner>[^/]+)/(?<repo>[^\\s]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GitHubScpPattern();
}
