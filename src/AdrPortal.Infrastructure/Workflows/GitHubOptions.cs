namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Represents GitHub integration defaults for ADR workflow operations.
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "GitHub";

    /// <summary>
    /// Gets or sets the fallback base branch when repository metadata cannot determine one.
    /// </summary>
    public string DefaultBaseBranch { get; set; } = "master";

    /// <summary>
    /// Gets or sets the commit author name used by automated Git operations.
    /// </summary>
    public string CommitAuthorName { get; set; } = "ADR Portal";

    /// <summary>
    /// Gets or sets the commit author email used by automated Git operations.
    /// </summary>
    public string CommitAuthorEmail { get; set; } = "adr-portal@localhost";

    /// <summary>
    /// Gets or sets the username used with token authentication for GitHub push operations.
    /// </summary>
    public string TokenUserName { get; set; } = "x-access-token";
}
