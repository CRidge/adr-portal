using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace AdrPortal.Web.Services;

/// <summary>
/// Imports markdown files from configured inbox folders into repository ADR storage.
/// </summary>
public sealed partial class InboxImportService(
    IManagedRepositoryStore managedRepositoryStore,
    IMadrRepositoryFactory madrRepositoryFactory,
    IMadrParser madrParser,
    ILogger<InboxImportService> logger) : IInboxImportService
{
    private static readonly Regex SlugInvalidCharactersRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ExistingAdrFileNameRegex = new(@"^adr-(?<number>\d{4})-(?<slug>[a-z0-9][a-z0-9-]*)\.md$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public async Task<InboxImportResult?> ImportInboxFileAsync(int repositoryId, string inboxFilePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(inboxFilePath))
        {
            throw new ArgumentException("Inbox file path is required.", nameof(inboxFilePath));
        }

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            logger.LogWarning("Inbox import skipped because repository {RepositoryId} was not found.", repositoryId);
            return null;
        }

        var inboxRootPath = RepositoryPathSecurity.ResolveInboxPath(repository);
        var resolvedFilePath = Path.GetFullPath(inboxFilePath);
        RepositoryPathSecurity.EnsurePathWithinRoot(
            resolvedFilePath,
            inboxRootPath,
            "Inbox file path escapes configured inbox folder.");

        var extension = Path.GetExtension(resolvedFilePath);
        if (!string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Inbox file '{resolvedFilePath}' must use the .md extension.");
        }

        if (!File.Exists(resolvedFilePath))
        {
            throw new FileNotFoundException($"Inbox file '{resolvedFilePath}' was not found.", resolvedFilePath);
        }

        var markdown = await ReadMarkdownFromPathAsync(resolvedFilePath, ct);
        var result = await ImportMarkdownInternalAsync(repository, Path.GetFileName(resolvedFilePath), markdown, ct);

        try
        {
            File.Delete(resolvedFilePath);
        }
        catch (IOException exception)
        {
            logger.LogWarning(
                exception,
                "Imported inbox file '{InboxFilePath}' but could not delete source file for repository {RepositoryId}.",
                resolvedFilePath,
                repositoryId);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(
                exception,
                "Imported inbox file '{InboxFilePath}' but delete was denied for repository {RepositoryId}.",
                resolvedFilePath,
                repositoryId);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<InboxImportResult?> ImportInboxMarkdownAsync(int repositoryId, string sourceFileName, string markdown, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            logger.LogWarning("Inbox markdown import skipped because repository {RepositoryId} was not found.", repositoryId);
            return null;
        }

        _ = RepositoryPathSecurity.ResolveInboxPath(repository);
        return await ImportMarkdownInternalAsync(repository, sourceFileName, markdown, ct);
    }

    private async Task<InboxImportResult> ImportMarkdownInternalAsync(
        ManagedRepository repository,
        string sourceFileName,
        string markdown,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source file name is required.", nameof(sourceFileName));
        }

        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new InvalidOperationException("Inbox markdown file is empty.");
        }

        var safeFileName = Path.GetFileName(sourceFileName);
        var extension = Path.GetExtension(safeFileName);
        if (!string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Inbox file '{safeFileName}' must use the .md extension.");
        }

        EnsureContentLooksLikeMarkdown(markdown);

        var adrRepository = madrRepositoryFactory.Create(repository);
        var nextNumber = await adrRepository.GetNextNumberAsync(ct);
        var normalizedAdrFolder = RepositoryPathSecurity.NormalizeRelativePath(repository.AdrFolder);
        var slug = BuildSlug(markdown, safeFileName, nextNumber);
        var repoRelativePath = $"{normalizedAdrFolder}/adr-{nextNumber:0000}-{slug}.md";

        var fallbackTitle = BuildTitleFromSlug(slug);
        var title = ExtractTitle(markdown) ?? fallbackTitle;
        var importAdr = new Adr
        {
            Number = nextNumber,
            Slug = slug,
            RepoRelativePath = repoRelativePath,
            Title = title,
            Status = AdrStatus.Proposed,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            DecisionMakers = ["Inbox import"],
            Consulted = [],
            Informed = [],
            RawMarkdown = markdown
        };

        importAdr = TryParseImportedAdr(importAdr, markdown);

        var persisted = await adrRepository.WriteAsync(importAdr, ct);
        logger.LogInformation(
            "Imported inbox markdown into ADR-{AdrNumber:0000} for repository {RepositoryId} from source '{SourceFileName}'.",
            persisted.Number,
            repository.Id,
            safeFileName);

        return new InboxImportResult
        {
            Repository = repository,
            ImportedAdr = persisted,
            Message = $"Imported {safeFileName} as ADR-{persisted.Number:0000} ({persisted.Slug})."
        };
    }

    private static async Task<string> ReadMarkdownFromPathAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length is 0)
        {
            throw new InvalidOperationException($"Inbox file '{path}' is empty.");
        }

        if (stream.Length > 1_000_000)
        {
            throw new InvalidOperationException($"Inbox file '{path}' exceeds the 1 MB limit.");
        }

        using var reader = new StreamReader(stream);
        var markdown = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new InvalidOperationException($"Inbox file '{path}' does not contain readable markdown content.");
        }

        return markdown;
    }

    private static void EnsureContentLooksLikeMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new InvalidOperationException("Inbox markdown content is empty.");
        }

        var lineEnd = markdown.IndexOf('\n');
        var firstLine = lineEnd >= 0 ? markdown[..lineEnd] : markdown;
        if (firstLine.IndexOf('\0') >= 0)
        {
            throw new InvalidOperationException("Inbox markdown content contains invalid binary bytes.");
        }
    }

    private static string BuildSlug(string markdown, string sourceFileName, int number)
    {
        var fileStem = Path.GetFileNameWithoutExtension(sourceFileName).Trim();
        if (ExistingAdrFileNameRegex.IsMatch(sourceFileName))
        {
            var existingMatch = ExistingAdrFileNameRegex.Match(sourceFileName);
            fileStem = existingMatch.Groups["slug"].Value;
        }

        var title = ExtractTitle(markdown);
        var seed = !string.IsNullOrWhiteSpace(title) ? title : fileStem;
        var lowered = seed.Trim().ToLowerInvariant();
        var collapsed = SlugInvalidCharactersRegex.Replace(lowered, "-").Trim('-');
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return $"inbox-adr-{number.ToString("0000", CultureInfo.InvariantCulture)}";
        }

        return collapsed;
    }

    private static string BuildTitleFromSlug(string slug)
    {
        var spaced = slug.Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }

    private static string? ExtractTitle(string markdown)
    {
        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[2..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private Adr TryParseImportedAdr(Adr fallbackAdr, string markdown)
    {
        try
        {
            var parsed = madrParser.Parse(fallbackAdr.RepoRelativePath, markdown);
            return parsed with
            {
                Number = fallbackAdr.Number,
                Slug = fallbackAdr.Slug,
                RepoRelativePath = fallbackAdr.RepoRelativePath,
                Status = AdrStatus.Proposed,
                SupersededByNumber = null,
                GlobalId = null,
                GlobalVersion = null
            };
        }
        catch (FormatException exception)
        {
            logger.LogInformation(
                exception,
                "Inbox markdown could not be parsed as complete MADR. Falling back to inferred metadata for '{RepoRelativePath}'.",
                fallbackAdr.RepoRelativePath);
            return fallbackAdr;
        }
        catch (ArgumentException exception)
        {
            logger.LogInformation(
                exception,
                "Inbox markdown parse failed due to input shape. Falling back to inferred metadata for '{RepoRelativePath}'.",
                fallbackAdr.RepoRelativePath);
            return fallbackAdr;
        }
    }

    /// <summary>
    /// Reads markdown content from a browser-uploaded stream.
    /// </summary>
    /// <param name="inputStream">Upload content stream.</param>
    /// <param name="maxBytes">Maximum allowed upload size in bytes.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Validated markdown content.</returns>
    public static async Task<string> ReadUploadMarkdownAsync(Stream inputStream, long maxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ct.ThrowIfCancellationRequested();

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Upload size limit must be greater than zero.");
        }

        using var buffer = new MemoryStream();
        await inputStream.CopyToAsync(buffer, 81920, ct);
        if (buffer.Length is 0)
        {
            throw new InvalidOperationException("Uploaded markdown file is empty.");
        }

        if (buffer.Length > maxBytes)
        {
            throw new InvalidOperationException($"Uploaded markdown file exceeds the {maxBytes} byte limit.");
        }

        var markdown = Encoding.UTF8.GetString(buffer.ToArray());
        EnsureContentLooksLikeMarkdown(markdown);
        return markdown;
    }
}
