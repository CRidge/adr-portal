using AdrPortal.Core.Entities;

namespace AdrPortal.Core.Tests;

public class ManagedRepositoryEntityTests
{
    [Test]
    [Arguments("docs/adr")]
    [Arguments("architecture/records")]
    public async Task ManagedRepository_SupportsConfiguredAdrFolder(string adrFolder)
    {
        var repository = new ManagedRepository
        {
            Id = 42,
            DisplayName = "Portal Repository",
            RootPath = @"C:\repos\adr",
            AdrFolder = adrFolder,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await Assert.That(repository.AdrFolder).IsEqualTo(adrFolder);
    }

    [Test]
    public async Task ManagedRepository_DefaultsMatchPhaseOneExpectations()
    {
        var repository = new ManagedRepository
        {
            DisplayName = "Defaulted Repository",
            RootPath = @"C:\repos\adr",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await Assert.That(repository.AdrFolder).IsEqualTo("docs/adr");
        await Assert.That(repository.IsActive).IsTrue();
        await Assert.That(repository.InboxFolder).IsNull();
        await Assert.That(repository.GitRemoteUrl).IsNull();
    }
}
