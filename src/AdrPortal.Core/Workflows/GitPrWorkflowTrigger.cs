namespace AdrPortal.Core.Workflows;

/// <summary>
/// Describes the workflow origin that requested Git/PR processing.
/// </summary>
public enum GitPrWorkflowTrigger
{
    /// <summary>
    /// Workflow triggered by ADR creation from AI bootstrap proposal acceptance.
    /// </summary>
    AiBootstrap = 0,

    /// <summary>
    /// Workflow triggered by ADR create/edit operations in the repository editor.
    /// </summary>
    RepositoryAdrPersist = 1,

    /// <summary>
    /// Workflow triggered by ADR lifecycle transition actions.
    /// </summary>
    RepositoryAdrTransition = 2,

    /// <summary>
    /// Workflow triggered by applying global library updates to a repository ADR instance.
    /// </summary>
    GlobalLibraryApply = 3
}
