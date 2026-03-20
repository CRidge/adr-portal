using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Tests;

public class ManagedRepositoryDefaultsTests
{
    [Test]
    public async Task InferFromGitRemoteUrl_UsesOwnerAndRepositoryForDisplayAndRootPath()
    {
        var inferred = ManagedRepositoryDefaults.InferFromGitRemoteUrl("https://github.com/octo/adr-portal.git");

        await Assert.That(inferred.GitRemoteUrl).IsEqualTo("https://github.com/octo/adr-portal.git");
        await Assert.That(inferred.DisplayName).IsEqualTo("octo/adr-portal");
        await Assert.That(inferred.RootPath).IsEqualTo(Path.Combine(ManagedRepositoryDefaults.DefaultRepositoriesRootPath, "octo", "adr-portal"));
        await Assert.That(inferred.AdrFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultAdrFolder);
        await Assert.That(inferred.InboxFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultInboxFolder);
    }

    [Test]
    public async Task InferFromGitRemoteUrl_UsesRepositoryFallbackWhenOwnerMissing()
    {
        var inferred = ManagedRepositoryDefaults.InferFromGitRemoteUrl("https://example.test/adr-portal.git");

        await Assert.That(inferred.DisplayName).IsEqualTo("adr-portal");
        await Assert.That(inferred.RootPath).IsEqualTo(Path.Combine(ManagedRepositoryDefaults.DefaultRepositoriesRootPath, "adr-portal"));
    }

    [Test]
    public async Task InferFromGitRemoteUrl_SupportsScpStyleRemoteSyntax()
    {
        var inferred = ManagedRepositoryDefaults.InferFromGitRemoteUrl("git@github.com:contoso/adr-platform.git");

        await Assert.That(inferred.DisplayName).IsEqualTo("contoso/adr-platform");
        await Assert.That(inferred.RootPath).IsEqualTo(Path.Combine(ManagedRepositoryDefaults.DefaultRepositoriesRootPath, "contoso", "adr-platform"));
    }

    [Test]
    public async Task CreateForAdd_WithUrlOnly_UsesInferredDefaults()
    {
        var created = ManagedRepositoryDefaults.CreateForAdd("https://github.com/fabrikam/decision-records.git");

        await Assert.That(created.GitRemoteUrl).IsEqualTo("https://github.com/fabrikam/decision-records.git");
        await Assert.That(created.DisplayName).IsEqualTo("fabrikam/decision-records");
        await Assert.That(created.RootPath).IsEqualTo(Path.Combine(ManagedRepositoryDefaults.DefaultRepositoriesRootPath, "fabrikam", "decision-records"));
        await Assert.That(created.AdrFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultAdrFolder);
        await Assert.That(created.InboxFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultInboxFolder);
        await Assert.That(created.IsActive).IsTrue();
    }

    [Test]
    public async Task CreateForAdd_AppliesOptionalDisplayNameAndActiveOverrides()
    {
        var created = ManagedRepositoryDefaults.CreateForAdd(
            gitRemoteUrl: "https://github.com/fabrikam/decision-records.git",
            displayNameOverride: "Portal Repo",
            isActive: false);

        await Assert.That(created.DisplayName).IsEqualTo("Portal Repo");
        await Assert.That(created.RootPath).IsEqualTo(Path.Combine(ManagedRepositoryDefaults.DefaultRepositoriesRootPath, "fabrikam", "decision-records"));
        await Assert.That(created.AdrFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultAdrFolder);
        await Assert.That(created.InboxFolder).IsEqualTo(ManagedRepositoryDefaults.DefaultInboxFolder);
        await Assert.That(created.IsActive).IsFalse();
    }
}
