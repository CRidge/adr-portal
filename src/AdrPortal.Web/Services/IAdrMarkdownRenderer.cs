namespace AdrPortal.Web.Services;

/// <summary>
/// Renders ADR markdown content as safe HTML for the UI.
/// </summary>
public interface IAdrMarkdownRenderer
{
    /// <summary>
    /// Renders markdown to HTML.
    /// </summary>
    /// <param name="markdown">Raw markdown content.</param>
    /// <returns>Rendered HTML content.</returns>
    string Render(string markdown);
}
