using AdrPortal.Core.Entities;
using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Workflows;
using LibGit2Sharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdrPortal.Infrastructure.Tests;

public class LibGit2SharpManagedRepositoryCheckoutServiceTests
{
    [Test]
    public async Task EnsureLocalCheckoutAsync_ClonesRepositoryWhenManagedPathMissing()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var (remotePath, _) = CreateSeededRemoteRepository(workspace);
            var checkoutPath = Path.Combine(workspace, "managed", "checkout");
            var repository = CreateManagedRepository(remotePath, checkoutPath);
            var service = CreateService();

            var resolvedPath = await service.EnsureLocalCheckoutAsync(repository, CancellationToken.None);

            await Assert.That(Repository.IsValid(resolvedPath)).IsTrue();
            await Assert.That(Path.GetFullPath(resolvedPath)).IsEqualTo(Path.GetFullPath(checkoutPath));
            await Assert.That(File.Exists(Path.Combine(checkoutPath, "README.md"))).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(workspace);
        }
    }

    [Test]
    public async Task RefreshCheckoutAsync_FastForwardsTrackedBranchWhenRemoteHasNewCommit()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var (remotePath, sourcePath) = CreateSeededRemoteRepository(workspace);
            var checkoutPath = Path.Combine(workspace, "managed", "checkout");
            var repository = CreateManagedRepository(remotePath, checkoutPath);
            var service = CreateService();
            _ = await service.EnsureLocalCheckoutAsync(repository, CancellationToken.None);
            var expectedHeadSha = CommitAndPush(sourcePath, "README.md", "# ADR Portal\n\nUpdated remotely.\n");

            _ = await service.RefreshCheckoutAsync(repository, CancellationToken.None);

            using var localRepository = new Repository(checkoutPath);
            await Assert.That(localRepository.Head.Tip).IsNotNull();
            await Assert.That(localRepository.Head.Tip!.Sha).IsEqualTo(expectedHeadSha);
        }
        finally
        {
            DeleteDirectoryIfExists(workspace);
        }
    }

    [Test]
    public async Task RefreshCheckoutAsync_ThrowsWhenRemoteAheadAndWorkingTreeHasLocalChanges()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var (remotePath, sourcePath) = CreateSeededRemoteRepository(workspace);
            var checkoutPath = Path.Combine(workspace, "managed", "checkout");
            var repository = CreateManagedRepository(remotePath, checkoutPath);
            var service = CreateService();
            _ = await service.EnsureLocalCheckoutAsync(repository, CancellationToken.None);
            _ = CommitAndPush(sourcePath, "README.md", "# ADR Portal\n\nUpdated remotely.\n");
            File.WriteAllText(Path.Combine(checkoutPath, "README.md"), "# ADR Portal\n\nLocal uncommitted change.\n");

            Exception? exception = null;
            try
            {
                _ = await service.RefreshCheckoutAsync(repository, CancellationToken.None);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            await Assert.That(exception is InvalidOperationException).IsTrue();
            await Assert.That(exception!.Message.Contains("local changes", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(workspace);
        }
    }

    private static IManagedRepositoryCheckoutService CreateService()
    {
        return new LibGit2SharpManagedRepositoryCheckoutService(
            NullLogger<LibGit2SharpManagedRepositoryCheckoutService>.Instance);
    }

    private static ManagedRepository CreateManagedRepository(string remotePath, string checkoutPath)
    {
        return new ManagedRepository
        {
            DisplayName = "contoso/adr-portal",
            RootPath = checkoutPath,
            AdrFolder = ManagedRepositoryDefaults.DefaultAdrFolder,
            InboxFolder = ManagedRepositoryDefaults.DefaultInboxFolder,
            GitRemoteUrl = remotePath,
            IsActive = true
        };
    }

    private static (string RemotePath, string SourcePath) CreateSeededRemoteRepository(string workspace)
    {
        var remotePath = Path.Combine(workspace, "remote.git");
        _ = Repository.Init(remotePath, isBare: true);

        var sourcePath = Path.Combine(workspace, "source");
        _ = Repository.Init(sourcePath);
        using var sourceRepository = new Repository(sourcePath);
        _ = sourceRepository.Network.Remotes.Add("origin", remotePath);

        _ = CommitAndPush(sourcePath, "README.md", "# ADR Portal\n\nInitial content.\n");
        return (remotePath, sourcePath);
    }

    private static string CommitAndPush(string sourcePath, string relativePath, string content)
    {
        using var sourceRepository = new Repository(sourcePath);
        var absolutePath = Path.Combine(sourcePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var parentDirectory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        File.WriteAllText(absolutePath, content);
        Commands.Stage(sourceRepository, relativePath.Replace('\\', '/'));

        var signature = new Signature("ADR Portal Tests", "tests@adrportal.local", DateTimeOffset.UtcNow);
        var commit = sourceRepository.Commit($"Update {relativePath}", signature, signature);
        var remote = sourceRepository.Network.Remotes["origin"]
            ?? throw new InvalidOperationException("Expected test repository to include an origin remote.");
        sourceRepository.Network.Push(
            remote,
            $"{sourceRepository.Head.CanonicalName}:{sourceRepository.Head.CanonicalName}",
            new PushOptions());
        return commit.Sha;
    }

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "adr-managed-checkout-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    ClearAttributesRecursively(path);
                    Directory.Delete(path, recursive: true);
                    return;
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    Thread.Sleep(100);
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }

    private static void ClearAttributesRecursively(string path)
    {
        var root = new DirectoryInfo(path);
        foreach (var directory in root.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            directory.Attributes = FileAttributes.Directory;
        }

        foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes = FileAttributes.Normal;
        }

        root.Attributes = FileAttributes.Directory;
    }
}
