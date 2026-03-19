namespace AdrPortal.Infrastructure.Ai;

/// <summary>
/// Defines configuration options for AI provider registration.
/// </summary>
public sealed record AiProviderOptions
{
    /// <summary>
    /// Configuration section name for AI options.
    /// </summary>
    public const string SectionName = "AI";

    /// <summary>
    /// Gets or sets configured provider identifier.
    /// </summary>
    public string Provider { get; set; } = "Deterministic";

    /// <summary>
    /// Gets or sets model name used by the external chat provider.
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Gets or sets base URL for compatible OpenAI endpoints.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets whether deterministic fallback should be used when external AI calls fail.
    /// </summary>
    public bool AllowDeterministicFallback { get; set; } = true;
}
