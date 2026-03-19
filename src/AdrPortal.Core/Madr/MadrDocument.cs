using System.Text.RegularExpressions;

namespace AdrPortal.Core.Madr;

internal static class MadrDocument
{
    private static readonly Regex FrontMatterRegex = new(
        @"\A---\r?\n(?<frontMatter>.*?)\r?\n---\r?\n?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static (string? FrontMatter, string Body) SplitFrontMatter(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var normalizedMarkdown = markdown.TrimStart('\uFEFF');
        var match = FrontMatterRegex.Match(normalizedMarkdown);
        if (!match.Success)
        {
            return (null, normalizedMarkdown);
        }

        var frontMatter = match.Groups["frontMatter"].Value;
        var body = normalizedMarkdown[match.Length..];

        return (frontMatter, body);
    }
}
