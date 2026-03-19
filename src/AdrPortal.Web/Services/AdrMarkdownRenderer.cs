using Markdig;
using System.Text.RegularExpressions;

namespace AdrPortal.Web.Services;

/// <summary>
/// Renders ADR markdown using a restricted Markdig pipeline.
/// </summary>
public sealed class AdrMarkdownRenderer : IAdrMarkdownRenderer
{
    private static readonly Regex FrontMatterRegex = new(
        @"\A---\r?\n(?<frontMatter>.*?)\r?\n---\r?\n?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseAutoLinks()
        .UseTaskLists()
        .Build();

    /// <summary>
    /// Renders markdown content to HTML while disabling raw HTML input.
    /// </summary>
    /// <param name="markdown">Raw markdown content.</param>
    /// <returns>Rendered HTML.</returns>
    public string Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var normalizedMarkdown = markdown.TrimStart('\uFEFF');
        var match = FrontMatterRegex.Match(normalizedMarkdown);
        var markdownBody = match.Success ? normalizedMarkdown[match.Length..] : normalizedMarkdown;

        return Markdown.ToHtml(markdownBody, Pipeline);
    }
}
