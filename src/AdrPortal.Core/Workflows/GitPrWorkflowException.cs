namespace AdrPortal.Core.Workflows;

/// <summary>
/// Represents a deterministic workflow error surfaced by Git/PR integration components.
/// </summary>
public sealed class GitPrWorkflowException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new exception with a specific message.
    /// </summary>
    /// <param name="message">Error message suitable for user feedback.</param>
    public GitPrWorkflowException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception with a specific message and inner exception.
    /// </summary>
    /// <param name="message">Error message suitable for user feedback.</param>
    /// <param name="innerException">Underlying exception details.</param>
    public GitPrWorkflowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
