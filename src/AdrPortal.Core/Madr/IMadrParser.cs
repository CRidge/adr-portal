using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Madr;

/// <summary>
/// Parses MADR markdown documents into ADR domain records.
/// </summary>
public interface IMadrParser
{
    /// <summary>
    /// Parses a MADR markdown document.
    /// </summary>
    /// <param name="repoRelativePath">Repository-relative path to the markdown file.</param>
    /// <param name="markdown">Full markdown document content.</param>
    /// <returns>The parsed ADR domain model.</returns>
    Adr Parse(string repoRelativePath, string markdown);
}
