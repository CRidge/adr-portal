namespace AdrPortal.Web.Services;

/// <summary>
/// Defines supported ADR lifecycle transition actions from the detail page.
/// </summary>
public enum AdrTransitionAction
{
    /// <summary>
    /// Accepts a proposed ADR.
    /// </summary>
    Accept = 0,

    /// <summary>
    /// Rejects a proposed ADR.
    /// </summary>
    Reject = 1,

    /// <summary>
    /// Marks an accepted ADR as superseded by another ADR number.
    /// </summary>
    Supersede = 2,

    /// <summary>
    /// Marks an accepted ADR as deprecated.
    /// </summary>
    Deprecate = 3
}
