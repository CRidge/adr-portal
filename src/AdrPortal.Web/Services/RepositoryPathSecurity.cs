using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Provides secure repository path resolution helpers for inbox and ADR operations.
/// </summary>
internal static class RepositoryPathSecurity
{
    /// <summary>
    /// Resolves and validates an inbox path for a managed repository.
    /// </summary>
    /// <param name="repository">Managed repository configuration.</param>
    /// <returns>Validated absolute inbox path.</returns>
    internal static string ResolveInboxPath(ManagedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (string.IsNullOrWhiteSpace(repository.InboxFolder))
        {
            throw new InvalidOperationException(
                $"Repository '{repository.DisplayName}' does not have an inbox folder configured.");
        }

        var inboxFolder = repository.InboxFolder.Trim();
        var candidatePath = Path.IsPathRooted(inboxFolder)
            ? inboxFolder
            : Path.Combine(repository.RootPath, inboxFolder);

        var inboxPath = Path.GetFullPath(candidatePath);
        EnsurePathWithinRoot(inboxPath, repository.RootPath, "Inbox folder escapes repository root.");
        return inboxPath;
    }

    /// <summary>
    /// Ensures a path remains within a configured repository root.
    /// </summary>
    /// <param name="path">Path to validate.</param>
    /// <param name="rootPath">Repository root path.</param>
    /// <param name="failureMessage">Exception message when validation fails.</param>
    internal static void EnsurePathWithinRoot(string path, string rootPath, string failureMessage)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedRoot = Path.GetFullPath(rootPath);
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        if (!normalizedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    /// <summary>
    /// Normalizes a relative path and rejects traversal segments.
    /// </summary>
    /// <param name="value">Relative path value.</param>
    /// <returns>Normalized path using forward slashes.</returns>
    internal static string NormalizeRelativePath(string value)
    {
        if (Path.IsPathRooted(value))
        {
            throw new InvalidOperationException("ADR folder path must be relative to repository root.");
        }

        var segments = value
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length is 0)
        {
            throw new InvalidOperationException("ADR folder path is required.");
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException("ADR folder path cannot contain traversal segments.");
        }

        return string.Join('/', segments);
    }
}
