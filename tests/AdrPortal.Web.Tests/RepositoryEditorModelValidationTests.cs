using System.ComponentModel.DataAnnotations;
using AdrPortal.Core.Entities;
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

public class AdrEditorModelValidationTests
{
    [Test]
    public async Task Validation_RequiresTitleAndDecisionMakers()
    {
        var model = AdrEditorModel.CreateForNew();
        model.Title = string.Empty;
        model.DecisionMakersText = string.Empty;

        var results = ValidateModel(model);

        await Assert.That(results.Any(result => result.MemberNames.Contains(nameof(AdrEditorModel.Title), StringComparer.Ordinal))).IsTrue();
        await Assert.That(results.Any(result => result.MemberNames.Contains(nameof(AdrEditorModel.DecisionMakersText), StringComparer.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Validation_RejectsInvalidSlug()
    {
        var model = AdrEditorModel.CreateForNew();
        model.Title = "Use PostgreSQL";
        model.Slug = "Use PostgreSQL";
        model.DecisionMakersText = "Architecture Board";

        var results = ValidateModel(model);

        await Assert.That(results.Any(result => result.MemberNames.Contains(nameof(AdrEditorModel.Slug), StringComparer.Ordinal))).IsTrue();
    }

    [Test]
    public async Task ToInput_ParsesPeopleFieldsAndNormalizesValues()
    {
        var model = AdrEditorModel.CreateForNew();
        model.Title = "  Introduce Caching  ";
        model.Slug = "introduce-caching";
        model.Status = AdrStatus.Accepted;
        model.Date = new DateOnly(2026, 3, 20);
        model.DecisionMakersText = "Board A,\nBoard B,\nBoard A";
        model.ConsultedText = "Security\nPlatform";
        model.InformedText = "Developers";
        model.SupersededByNumber = 7;
        model.MarkdownBody = "## Context and Problem Statement";

        var input = model.ToInput();

        await Assert.That(input.Title).IsEqualTo("Introduce Caching");
        await Assert.That(input.Status).IsEqualTo(AdrStatus.Accepted);
        await Assert.That(input.DecisionMakers.Count).IsEqualTo(2);
        await Assert.That(input.DecisionMakers[0]).IsEqualTo("Board A");
        await Assert.That(input.DecisionMakers[1]).IsEqualTo("Board B");
        await Assert.That(input.Consulted.Count).IsEqualTo(2);
        await Assert.That(input.SupersededByNumber).IsEqualTo(7);
    }

    [Test]
    public async Task FromAdr_RemovesFrontMatterAndCopiesMetadata()
    {
        var adr = new Adr
        {
            Number = 4,
            Slug = "introduce-cache",
            RepoRelativePath = "docs/adr/adr-0004-introduce-cache.md",
            Title = "Introduce Cache",
            Status = AdrStatus.Superseded,
            Date = new DateOnly(2026, 3, 19),
            DecisionMakers = ["Board"],
            Consulted = ["Security"],
            Informed = ["Developers"],
            SupersededByNumber = 9,
            RawMarkdown = """
---
status: superseded
date: 2026-03-19
decision-makers: [Board]
consulted: [Security]
informed: [Developers]
superseded-by: 9
---
# Introduce Cache

## Context and Problem Statement
"""
        };

        var model = AdrEditorModel.FromAdr(adr);

        await Assert.That(model.Title).IsEqualTo("Introduce Cache");
        await Assert.That(model.Status).IsEqualTo(AdrStatus.Superseded);
        await Assert.That(model.MarkdownBody.StartsWith("# Introduce Cache", StringComparison.Ordinal)).IsTrue();
        await Assert.That(model.MarkdownBody.Contains("status:", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    private static IReadOnlyList<ValidationResult> ValidateModel(AdrEditorModel model)
    {
        var validationContext = new ValidationContext(model);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(model, validationContext, results, validateAllProperties: true);
        return results;
    }
}
