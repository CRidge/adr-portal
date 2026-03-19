using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Handles uploaded inbox markdown documents for repository routes.
/// </summary>
public sealed class RepositoryInboxUploadService(IInboxImportService inboxImportService)
{
    /// <summary>
    /// Imports uploaded markdown documents into repository ADR storage.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="uploads">Uploaded markdown documents.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Upload processing outcome with imported ADR numbers and failures.</returns>
    public async Task<RepositoryInboxUploadOutcome> ImportAsync(
        int repositoryId,
        IReadOnlyCollection<RepositoryInboxUploadDocument> uploads,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        ct.ThrowIfCancellationRequested();

        if (uploads.Count is 0)
        {
            throw new InvalidOperationException("No upload files were provided.");
        }

        var importedAdrs = new List<Adr>(uploads.Count);
        var failures = new List<string>();
        foreach (var upload in uploads)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var safeFileName = Path.GetFileName(upload.FileName);
                if (string.IsNullOrWhiteSpace(safeFileName))
                {
                    throw new ArgumentException("Upload file name is required.", nameof(uploads));
                }

                if (!string.Equals(Path.GetExtension(safeFileName), ".md", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"'{safeFileName}' is not a .md file.");
                }

                var result = await inboxImportService.ImportInboxMarkdownAsync(repositoryId, safeFileName, upload.Markdown, ct);
                if (result is null)
                {
                    failures.Add($"Repository '{repositoryId}' was not found for '{safeFileName}'.");
                    continue;
                }

                importedAdrs.Add(result.ImportedAdr);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add(exception.Message);
            }
            catch (ArgumentException exception)
            {
                failures.Add(exception.Message);
            }
            catch (FormatException exception)
            {
                failures.Add(exception.Message);
            }
            catch (IOException exception)
            {
                failures.Add(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                failures.Add(exception.Message);
            }
        }

        return new RepositoryInboxUploadOutcome
        {
            ImportedAdrs = importedAdrs,
            Failures = failures
        };
    }
}

/// <summary>
/// Represents a markdown document uploaded to repository inbox processing.
/// </summary>
/// <param name="FileName">Original upload file name.</param>
/// <param name="Markdown">Uploaded markdown content.</param>
public sealed record RepositoryInboxUploadDocument(string FileName, string Markdown);

/// <summary>
/// Represents upload processing results for repository inbox imports.
/// </summary>
public sealed record RepositoryInboxUploadOutcome
{
    /// <summary>
    /// Gets ADRs imported from uploads.
    /// </summary>
    public required IReadOnlyList<Adr> ImportedAdrs { get; init; }

    /// <summary>
    /// Gets upload failure messages.
    /// </summary>
    public required IReadOnlyList<string> Failures { get; init; }
}
