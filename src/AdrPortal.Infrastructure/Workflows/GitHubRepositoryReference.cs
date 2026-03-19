namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Represents a parsed GitHub owner/repository pair.
/// </summary>
internal sealed record GitHubRepositoryReference(string Owner, string Repository);
