using System.ComponentModel.DataAnnotations;
using AdrPortal.Web.Components.Pages;

namespace AdrPortal.Web.Tests;

public class RepositoryEditorModelValidationTests
{
    [Test]
    public async Task Validation_RequiresGitRemoteUrl()
    {
        var model = new RepositoryEditorModel
        {
            GitRemoteUrl = string.Empty
        };

        var results = ValidateModel(model);

        await Assert.That(results.Any(result => result.MemberNames.Contains(nameof(RepositoryEditorModel.GitRemoteUrl), StringComparer.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validation_AllowsUrlOnlyInput()
    {
        var model = new RepositoryEditorModel
        {
            GitRemoteUrl = "https://github.com/contoso/adr-portal.git",
            DisplayName = string.Empty,
            RootPath = string.Empty,
            AdrFolder = string.Empty,
            InboxFolder = null
        };

        var results = ValidateModel(model);

        await Assert.That(results.Count).IsEqualTo(0);
    }

    private static IReadOnlyList<ValidationResult> ValidateModel(RepositoryEditorModel model)
    {
        var validationContext = new ValidationContext(model);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(model, validationContext, results, validateAllProperties: true);
        return results;
    }
}
