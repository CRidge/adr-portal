using AdrPortal.Core.Workflows;
using Microsoft.Extensions.Options;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Resolves Git credentials from environment variables with deterministic fallback behavior.
/// </summary>
public sealed class EnvironmentGitCredentialResolver(IOptions<GitHubOptions> options) : IGitCredentialResolver
{
    /// <summary>
    /// Resolves credentials required by Git and GitHub integration.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Resolved credential model.</returns>
    /// <exception cref="GitPrWorkflowException">Thrown when required credentials are not configured.</exception>
    public Task<GitCredentialResolution> ResolveAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(githubToken))
        {
            throw new GitPrWorkflowException(
                "GitHub integration requires GITHUB_TOKEN. Configure it in the environment for least-privilege PR automation.");
        }

        var resolved = new GitCredentialResolution
        {
            GitHubToken = githubToken.Trim(),
            GitUserName = options.Value.TokenUserName,
            GitPassword = githubToken.Trim()
        };

        return Task.FromResult(resolved);
    }
}
