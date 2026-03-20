using AdrPortal.Core.Entities;
using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Workflows;
using Microsoft.Extensions.Options;

namespace AdrPortal.Infrastructure.Tests;

public class LibGit2SharpRepositoryServiceCheckoutTests
{
    [Test]
    public async Task EnsureRepositoryReadyAsync_ThrowsWhenGitRemoteIsMissing()
    {
        var service = new LibGit2SharpRepositoryService(Options.Create(new GitHubOptions()));
        var repository = new ManagedRepository
        {
            Id = 1,
            DisplayName = "contoso/portal",
            RootPath = Path.Combine(Path.GetTempPath(), "adr-phase-feedback-missing-remote", Guid.NewGuid().ToString("N")),
            AdrFolder = ManagedRepositoryDefaults.DefaultAdrFolder,
            InboxFolder = ManagedRepositoryDefaults.DefaultInboxFolder,
            GitRemoteUrl = null,
            IsActive = true
        };

        Exception? exception = null;
        try
        {
            await service.EnsureRepositoryReadyAsync(repository, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
        await Assert.That(exception!.Message.Contains("required", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task EnsureRepositoryReadyAsync_ThrowsWhenRepositoryCannotBeCloned()
    {
        var service = new LibGit2SharpRepositoryService(Options.Create(new GitHubOptions()));
        var root = Path.Combine(Path.GetTempPath(), "adr-phase-feedback-bad-clone", Guid.NewGuid().ToString("N"));
        var repository = new ManagedRepository
        {
            Id = 2,
            DisplayName = "contoso/portal",
            RootPath = root,
            AdrFolder = ManagedRepositoryDefaults.DefaultAdrFolder,
            InboxFolder = ManagedRepositoryDefaults.DefaultInboxFolder,
            GitRemoteUrl = "https://127.0.0.1:1/non-existent/repo.git",
            IsActive = true
        };

        Exception? exception = null;
        try
        {
            await service.EnsureRepositoryReadyAsync(repository, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("Failed to clone repository", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task EnsureRepositoryReadyAsync_ThrowsWhenNonGitRootContainsFiles()
    {
        var service = new LibGit2SharpRepositoryService(Options.Create(new GitHubOptions()));
        var root = Path.Combine(Path.GetTempPath(), "adr-phase-feedback-non-git-files", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "seed.txt"), "seed");

        var repository = new ManagedRepository
        {
            Id = 3,
            DisplayName = "contoso/portal",
            RootPath = root,
            AdrFolder = ManagedRepositoryDefaults.DefaultAdrFolder,
            InboxFolder = ManagedRepositoryDefaults.DefaultInboxFolder,
            GitRemoteUrl = "https://github.com/contoso/portal.git",
            IsActive = true
        };

        Exception? exception = null;
        try
        {
            await service.EnsureRepositoryReadyAsync(repository, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("contains files", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
