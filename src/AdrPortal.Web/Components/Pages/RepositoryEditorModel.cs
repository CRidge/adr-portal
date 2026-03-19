using System.ComponentModel.DataAnnotations;

namespace AdrPortal.Web.Components.Pages;

/// <summary>
/// Validated input model for creating and updating managed repositories.
/// </summary>
public sealed class RepositoryEditorModel
{
    /// <summary>
    /// Gets or sets the display name shown in portal UI.
    /// </summary>
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute repository root path.
    /// </summary>
    [StringLength(1024)]
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative folder that contains ADR markdown files.
    /// </summary>
    [StringLength(260)]
    public string AdrFolder { get; set; } = "docs/adr";

    /// <summary>
    /// Gets or sets the optional inbox folder for imported files.
    /// </summary>
    [StringLength(1024)]
    public string? InboxFolder { get; set; }

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
