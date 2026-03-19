using System.Text.RegularExpressions;

namespace AdrPortal.Core.Entities;

/// <summary>
/// Provides deterministic defaults for managed repository settings derived from a Git remote URL.
/// </summary>
public static partial class ManagedRepositoryDefaults
{
    /// <summary>
    /// The default relative ADR folder used when no override is provided.
    /// </summary>
    public const string DefaultAdrFolder = "docs/adr";

    /// <summary>
    /// The deterministic root directory used when inferring local repository paths.
    /// </summary>
    public const string DefaultRepositoriesRootPath = @"C:\repos";

    /// <summary>
    /// Creates a managed repository for add flow using a required Git remote URL and optional overrides.
    /// </summary>
    /// <param name="gitRemoteUrl">Git remote URL used as the primary source of defaults.</param>
    /// <param name="displayNameOverride">Optional display name override. When empty, an inferred value is used.</param>
    /// <param name="rootPathOverride">Optional root path override. When empty, an inferred value is used.</param>
    /// <param name="adrFolderOverride">Optional ADR folder override. When empty, <c>docs/adr</c> is used.</param>
    /// <param name="inboxFolderOverride">Optional inbox folder override.</param>
    /// <param name="isActive">Whether the created repository should be active.</param>
    /// <returns>A managed repository populated with inferred defaults and applied overrides.</returns>
    public static ManagedRepository CreateForAdd(
        string gitRemoteUrl,
        string? displayNameOverride = null,
        string? rootPathOverride = null,
        string? adrFolderOverride = null,
        string? inboxFolderOverride = null,
        bool isActive = true)
    {
        var inferredDefaults = InferFromGitRemoteUrl(gitRemoteUrl);

        return new ManagedRepository
        {
            DisplayName = ResolveRequiredValue(displayNameOverride, inferredDefaults.DisplayName),
            RootPath = ResolveRequiredValue(rootPathOverride, inferredDefaults.RootPath),
            AdrFolder = ResolveRequiredValue(adrFolderOverride, inferredDefaults.AdrFolder),
            InboxFolder = ResolveOptionalValue(inboxFolderOverride, inferredDefaults.InboxFolder),
            GitRemoteUrl = inferredDefaults.GitRemoteUrl,
            IsActive = isActive
        };
    }

    /// <summary>
    /// Infers default repository fields from a Git remote URL.
    /// </summary>
    /// <param name="gitRemoteUrl">Git remote URL in HTTPS, SSH, or SCP-like syntax.</param>
    /// <returns>Inferred defaults for display name, root path, ADR folder, and inbox folder.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL cannot provide a repository segment.</exception>
    public static ManagedRepositoryDefaultsResult InferFromGitRemoteUrl(string gitRemoteUrl)
    {
        var normalizedUrl = NormalizeGitRemoteUrl(gitRemoteUrl);
        var pathSegments = ExtractPathSegments(normalizedUrl);

        if (pathSegments.Count is 0)
        {
            throw new ArgumentException("Git remote URL must include a repository path segment.", nameof(gitRemoteUrl));
        }

        var repositorySegment = TrimGitSuffix(pathSegments[^1]);
        if (string.IsNullOrWhiteSpace(repositorySegment))
        {
            throw new ArgumentException("Git remote URL must include a repository name segment.", nameof(gitRemoteUrl));
        }

        var ownerSegment = pathSegments.Count > 1 ? pathSegments[^2] : null;
        var displayName = ownerSegment is null
            ? repositorySegment
            : $"{ownerSegment}/{repositorySegment}";

        var inferredRootPath = ownerSegment is null
            ? Path.Combine(DefaultRepositoriesRootPath, SanitizePathSegment(repositorySegment))
            : Path.Combine(
                DefaultRepositoriesRootPath,
                SanitizePathSegment(ownerSegment),
                SanitizePathSegment(repositorySegment));

        return new ManagedRepositoryDefaultsResult(
            GitRemoteUrl: normalizedUrl,
            DisplayName: displayName,
            RootPath: inferredRootPath,
            AdrFolder: DefaultAdrFolder,
            InboxFolder: null);
    }

    /// <summary>
    /// Normalizes and validates the Git remote URL input.
    /// </summary>
    /// <param name="gitRemoteUrl">Raw input URL.</param>
    /// <returns>Trimmed URL string.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is missing.</exception>
    private static string NormalizeGitRemoteUrl(string gitRemoteUrl)
    {
        if (string.IsNullOrWhiteSpace(gitRemoteUrl))
        {
            throw new ArgumentException("Git remote URL is required.", nameof(gitRemoteUrl));
        }

        return gitRemoteUrl.Trim();
    }

    /// <summary>
    /// Extracts path segments from supported Git remote URL formats.
    /// </summary>
    /// <param name="normalizedGitRemoteUrl">Trimmed Git remote URL.</param>
    /// <returns>Path segments containing owner and repository identifiers.</returns>
    private static IReadOnlyList<string> ExtractPathSegments(string normalizedGitRemoteUrl)
    {
        string? remotePath = null;
        if (Uri.TryCreate(normalizedGitRemoteUrl, UriKind.Absolute, out var remoteUri))
        {
            remotePath = remoteUri.AbsolutePath;
        }
        else
        {
            var scpMatch = ScpStyleRemotePattern().Match(normalizedGitRemoteUrl);
            if (scpMatch.Success)
            {
                remotePath = scpMatch.Groups["path"].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return [];
        }

        return remotePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    /// <summary>
    /// Trims a trailing <c>.git</c> suffix from the repository segment when present.
    /// </summary>
    /// <param name="segment">Repository segment.</param>
    /// <returns>Repository segment without a trailing <c>.git</c> suffix.</returns>
    private static string TrimGitSuffix(string segment)
    {
        if (segment.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            return segment[..^4];
        }

        return segment;
    }

    /// <summary>
    /// Sanitizes an inferred path segment for use in Windows directory names.
    /// </summary>
    /// <param name="segment">Raw inferred segment.</param>
    /// <returns>Sanitized path-safe segment.</returns>
    /// <exception cref="ArgumentException">Thrown when the segment becomes empty after sanitization.</exception>
    private static string SanitizePathSegment(string segment)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(segment.Where(character => Array.IndexOf(invalidCharacters, character) < 0).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Git remote URL contains an invalid repository segment.", nameof(segment));
        }

        return sanitized;
    }

    /// <summary>
    /// Resolves a required value by applying an override when present or falling back to a default.
    /// </summary>
    /// <param name="overrideValue">Optional override value.</param>
    /// <param name="defaultValue">Default value used when the override is empty.</param>
    /// <returns>The resolved required value.</returns>
    private static string ResolveRequiredValue(string? overrideValue, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(overrideValue) ? defaultValue : overrideValue.Trim();
    }

    /// <summary>
    /// Resolves an optional value by applying an override when present or falling back to a default.
    /// </summary>
    /// <param name="overrideValue">Optional override value.</param>
    /// <param name="defaultValue">Default value used when the override is empty.</param>
    /// <returns>The resolved optional value.</returns>
    private static string? ResolveOptionalValue(string? overrideValue, string? defaultValue)
    {
        return string.IsNullOrWhiteSpace(overrideValue) ? defaultValue : overrideValue.Trim();
    }

    /// <summary>
    /// Provides a compiled regular expression for SCP-like Git remotes such as <c>git@host:owner/repo.git</c>.
    /// </summary>
    /// <returns>Compiled regular expression for SCP-like remotes.</returns>
    [GeneratedRegex("^[^@\\s]+@[^:\\s]+:(?<path>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpStyleRemotePattern();
}

/// <summary>
/// Represents inferred defaults derived from a Git remote URL.
/// </summary>
/// <param name="GitRemoteUrl">Normalized Git remote URL.</param>
/// <param name="DisplayName">Inferred display name.</param>
/// <param name="RootPath">Inferred deterministic root path.</param>
/// <param name="AdrFolder">Inferred ADR folder.</param>
/// <param name="InboxFolder">Inferred inbox folder.</param>
public sealed record ManagedRepositoryDefaultsResult(
    string GitRemoteUrl,
    string DisplayName,
    string RootPath,
    string AdrFolder,
    string? InboxFolder);
