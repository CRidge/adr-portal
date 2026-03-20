using System.ComponentModel.DataAnnotations;

namespace AdrPortal.Web.Components.Pages;

/// <summary>
/// Validated input model for creating and updating managed repositories.
/// </summary>
public sealed class RepositoryEditorModel
{
    /// <summary>
    /// Gets or sets the optional display name override shown in portal UI.
    /// </summary>
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
     /// Gets or sets the required Git remote URL.
     /// </summary>
    [Required]
    [StringLength(2048)]
    public string GitRemoteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the repository should be active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
