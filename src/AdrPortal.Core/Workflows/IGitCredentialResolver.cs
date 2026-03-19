namespace AdrPortal.Core.Workflows;

/// <summary>
/// Resolves credentials required for authenticated Git and GitHub operations.
/// </summary>
public interface IGitCredentialResolver
{
    /// <summary>
    /// Resolves GitHub token and optional Git push credentials.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Resolved credential set.</returns>
    Task<GitCredentialResolution> ResolveAsync(CancellationToken ct);
}
