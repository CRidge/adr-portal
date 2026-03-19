namespace AdrPortal.Core.Entities;

/// <summary>
/// Represents lifecycle states for an ADR document.
/// </summary>
public enum AdrStatus
{
    Proposed = 0,
    Accepted = 1,
    Rejected = 2,
    Superseded = 3,
    Deprecated = 4
}
