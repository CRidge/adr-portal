using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Core.Repositories;

namespace AdrPortal.Infrastructure.Repositories;

/// <summary>
/// Stores ADR markdown files within a repository folder structure.
/// </summary>
public sealed class AdrFileRepository : IAdrFileRepository
{
    private static readonly Regex FileNameRegex = new(
        @"^adr-(?<number>\d{4})-(?<slug>[a-z0-9][a-z0-9-]*)\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly IMadrParser madrParser;
    private readonly IMadrWriter madrWriter;
    private readonly string repositoryRootPath;
    private readonly string adrFolderRelativePath;
    private readonly string adrFolderAbsolutePath;
    private readonly string rejectedFolderAbsolutePath;

    /// <summary>
    /// Initializes a file-backed ADR repository for a single managed source repository.
    /// </summary>
    /// <param name="repositoryRootPath">Absolute repository root path.</param>
    /// <param name="adrFolderRelativePath">Relative ADR folder path under the repository root.</param>
    /// <param name="madrParser">MADR parser used when loading files.</param>
    /// <param name="madrWriter">MADR writer used when persisting files.</param>
    public AdrFileRepository(
        string repositoryRootPath,
        string adrFolderRelativePath,
        IMadrParser madrParser,
        IMadrWriter madrWriter)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            throw new ArgumentException("Repository root path is required.", nameof(repositoryRootPath));
        }

        if (string.IsNullOrWhiteSpace(adrFolderRelativePath))
        {
            throw new ArgumentException("ADR folder path is required.", nameof(adrFolderRelativePath));
        }

        this.madrParser = madrParser ?? throw new ArgumentNullException(nameof(madrParser));
        this.madrWriter = madrWriter ?? throw new ArgumentNullException(nameof(madrWriter));
        this.repositoryRootPath = Path.GetFullPath(repositoryRootPath);
        this.adrFolderRelativePath = NormalizeRelativePath(adrFolderRelativePath);
        this.adrFolderAbsolutePath = BuildAbsolutePath(this.adrFolderRelativePath);
        this.rejectedFolderAbsolutePath = Path.Combine(this.adrFolderAbsolutePath, "rejected");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var adrs = new List<Adr>();
        foreach (var filePath in EnumerateAdrFilePaths())
        {
            ct.ThrowIfCancellationRequested();

            var repoRelativePath = BuildRepoRelativePath(filePath);
            var markdown = await File.ReadAllTextAsync(filePath, ct);
            var adr = madrParser.Parse(repoRelativePath, markdown);
            adrs.Add(adr);
        }

        return adrs
            .OrderBy(adr => adr.Number)
            .ThenBy(adr => adr.RepoRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "ADR number must be greater than zero.");
        }

        var filePath = EnumerateAdrFilePaths(number)
            .OrderBy(path => IsRejectedPath(path))
            .FirstOrDefault();

        if (filePath is null)
        {
            return null;
        }

        var repoRelativePath = BuildRepoRelativePath(filePath);
        var markdown = await File.ReadAllTextAsync(filePath, ct);

        return madrParser.Parse(repoRelativePath, markdown);
    }

    /// <inheritdoc />
    public async Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(adr);
        ct.ThrowIfCancellationRequested();

        if (adr.Number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(adr), "ADR number must be greater than zero.");
        }

        if (!FileNameRegex.IsMatch($"adr-{adr.Number:0000}-{adr.Slug}.md"))
        {
            throw new ArgumentException($"ADR slug '{adr.Slug}' is invalid.", nameof(adr));
        }

        var targetRelativePath = BuildRelativePathForStatus(adr.Number, adr.Slug, adr.Status);
        var targetAbsolutePath = BuildAbsolutePath(targetRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(targetAbsolutePath)!);

        var markdown = madrWriter.Write(adr with { RepoRelativePath = targetRelativePath });
        await File.WriteAllTextAsync(targetAbsolutePath, markdown, Utf8NoBom, ct);

        await DeleteConflictingFilesAsync(adr.Number, targetAbsolutePath, ct);

        var persistedMarkdown = await File.ReadAllTextAsync(targetAbsolutePath, ct);
        return madrParser.Parse(targetRelativePath, persistedMarkdown);
    }

    /// <inheritdoc />
    public async Task MoveToRejectedAsync(int number, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var existingAdr = await GetByNumberAsync(number, ct);
        if (existingAdr is null)
        {
            throw new FileNotFoundException($"Unable to find ADR {number:0000}.");
        }

        _ = await WriteAsync(existingAdr with { Status = AdrStatus.Rejected }, ct);
    }

    /// <inheritdoc />
    public Task<int> GetNextNumberAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var maxNumber = EnumerateAdrFilePaths()
            .Select(GetNumberFromPath)
            .DefaultIfEmpty(0)
            .Max();

        return Task.FromResult(maxNumber + 1);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("ADR folder path must be relative to the repository root.", nameof(relativePath));
        }

        var segments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (segments.Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException("ADR folder path cannot contain relative traversal segments.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    private string BuildRelativePathForStatus(int number, string slug, AdrStatus status)
    {
        var fileName = $"adr-{number:0000}-{slug.ToLowerInvariant()}.md";
        return status is AdrStatus.Rejected
            ? $"{adrFolderRelativePath}/rejected/{fileName}"
            : $"{adrFolderRelativePath}/{fileName}";
    }

    private string BuildAbsolutePath(string repoRelativePath)
    {
        var osRelativePath = repoRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var combinedPath = Path.GetFullPath(Path.Combine(repositoryRootPath, osRelativePath));

        var rootWithSeparator = repositoryRootPath.EndsWith(Path.DirectorySeparatorChar)
            ? repositoryRootPath
            : repositoryRootPath + Path.DirectorySeparatorChar;

        if (!combinedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combinedPath, repositoryRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Computed ADR path escapes the repository root.");
        }

        return combinedPath;
    }

    private string BuildRepoRelativePath(string absolutePath)
    {
        var relativePath = Path.GetRelativePath(repositoryRootPath, absolutePath);
        return NormalizeRelativePath(relativePath);
    }

    private IEnumerable<string> EnumerateAdrFilePaths(int? numberFilter = null)
    {
        foreach (var searchPath in GetSearchPaths())
        {
            if (!Directory.Exists(searchPath))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(searchPath, "adr-*.md", SearchOption.TopDirectoryOnly))
            {
                if (!TryExtractNumber(filePath, out var fileNumber))
                {
                    continue;
                }

                if (numberFilter is not null && fileNumber != numberFilter.Value)
                {
                    continue;
                }

                yield return filePath;
            }
        }
    }

    private IEnumerable<string> GetSearchPaths()
    {
        yield return adrFolderAbsolutePath;
        yield return rejectedFolderAbsolutePath;
    }

    private static bool IsRejectedPath(string filePath)
    {
        var fileDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
        return fileDirectory.EndsWith("rejected", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractNumber(string path, out int number)
    {
        var fileName = Path.GetFileName(path);
        var match = FileNameRegex.Match(fileName);
        if (!match.Success)
        {
            number = 0;
            return false;
        }

        number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        return true;
    }

    private static int GetNumberFromPath(string path)
    {
        if (TryExtractNumber(path, out var number))
        {
            return number;
        }

        return 0;
    }

    private async Task DeleteConflictingFilesAsync(int number, string keepPath, CancellationToken ct)
    {
        var conflictingFiles = EnumerateAdrFilePaths(number)
            .Where(path => !string.Equals(path, keepPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var conflictingFile in conflictingFiles)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Run(() => File.Delete(conflictingFile), ct);
        }
    }
}
