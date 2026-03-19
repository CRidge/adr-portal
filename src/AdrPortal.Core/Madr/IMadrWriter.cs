using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Madr;

/// <summary>
/// Serializes ADR domain records into MADR markdown documents.
/// </summary>
public interface IMadrWriter
{
    /// <summary>
    /// Serializes an ADR to markdown.
    /// </summary>
    /// <param name="adr">ADR domain model to write.</param>
    /// <returns>The MADR markdown content.</returns>
    string Write(Adr adr);
}
