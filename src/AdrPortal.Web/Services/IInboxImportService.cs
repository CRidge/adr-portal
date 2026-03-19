using AdrPortal.Core.Entities;

namespace AdrPortal.Web.Services;

/// <summary>
/// Imports markdown files from repository inbox sources into ADR storage.
/// </summary>
public interface IInboxImportService
{
    /// <summary>
    /// Imports an inbox markdown file from disk for the specified repository.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="inboxFilePath">Absolute path of the inbox file to import.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The import result when repository exists; otherwise <see langword="null"/>.</returns>
    Task<InboxImportResult?> ImportInboxFileAsync(int repositoryId, string inboxFilePath, CancellationToken ct);

    /// <summary>
    /// Imports markdown content supplied by another input channel such as file upload.
    /// </summary>
    /// <param name="repositoryId">Managed repository identifier.</param>
    /// <param name="sourceFileName">Original file name used for slug inference and validation.</param>
    /// <param name="markdown">Markdown content to import.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>The import result when repository exists; otherwise <see langword="null"/>.</returns>
    Task<InboxImportResult?> ImportInboxMarkdownAsync(int repositoryId, string sourceFileName, string markdown, CancellationToken ct);
}
