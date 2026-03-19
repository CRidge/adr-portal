namespace AdrPortal.Core.Workflows;

/// <summary>
/// Represents credentials resolved for Git/PR integration.
/// </summary>
public sealed record GitCredentialResolution
{
    /// <summary>
    /// Gets the resolved GitHub token used for API operations.
    /// </summary>
    public required string GitHubToken { get; init; }

    /// <summary>
    /// Gets the optional username used for Git remote push.
    /// </summary>
    public string? GitUserName { get; init; }

    /// <summary>
    /// Gets the optional password or token used for Git remote push.
    /// </summary>
    public string? GitPassword { get; init; }
}
