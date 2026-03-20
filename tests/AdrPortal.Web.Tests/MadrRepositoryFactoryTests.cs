using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Web.Services;
using LibGit2Sharp;

namespace AdrPortal.Web.Tests;

public class MadrRepositoryFactoryTests
{
    [Test]
    public async Task Create_ClonesRemoteIntoManagedCheckout_AndReadsFromFixedAdrFolder()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var remoteRepositoryPath = Path.Combine(root, "remote.git");
            var seedRepositoryPath = Path.Combine(root, "seed");
            InitializeSeedAndRemote(seedRepositoryPath, remoteRepositoryPath);
            AddAdrCommitAndPush(seedRepositoryPath, remoteRepositoryPath, number: 1, slug: "use-events", title: "Use Events");

            var managedCheckoutPath = Path.Combine(root, "managed", "contoso", "adr-portal");
            var managedRepository = new ManagedRepository
            {
                Id = 1,
                DisplayName = "contoso/adr-portal",
                RootPath = managedCheckoutPath,
                AdrFolder = "custom/adr/location",
                InboxFolder = "custom/inbox/location",
                GitRemoteUrl = remoteRepositoryPath,
                IsActive = true
            };

            var factory = new MadrRepositoryFactory(new MadrParser(), new MadrWriter());
            var adrRepository = factory.Create(managedRepository);
            var adrs = await adrRepository.GetAllAsync(CancellationToken.None);

            await Assert.That(Repository.IsValid(managedCheckoutPath)).IsTrue();
            await Assert.That(adrs.Count).IsEqualTo(1);
            await Assert.That(adrs[0].Number).IsEqualTo(1);
            await Assert.That(adrs[0].RepoRelativePath.StartsWith("docs/adr/", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task Create_UpdatesExistingManagedCheckout_BeforeAdrDiscovery()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var remoteRepositoryPath = Path.Combine(root, "remote.git");
            var seedRepositoryPath = Path.Combine(root, "seed");
            InitializeSeedAndRemote(seedRepositoryPath, remoteRepositoryPath);
            AddAdrCommitAndPush(seedRepositoryPath, remoteRepositoryPath, number: 1, slug: "use-events", title: "Use Events");

            var managedCheckoutPath = Path.Combine(root, "managed", "contoso", "adr-portal");
            var managedRepository = new ManagedRepository
            {
                Id = 2,
                DisplayName = "contoso/adr-portal",
                RootPath = managedCheckoutPath,
                AdrFolder = ManagedRepositoryDefaults.DefaultAdrFolder,
                InboxFolder = ManagedRepositoryDefaults.DefaultInboxFolder,
                GitRemoteUrl = remoteRepositoryPath,
                IsActive = true
            };

            var factory = new MadrRepositoryFactory(new MadrParser(), new MadrWriter());
            var firstDiscovery = await factory.Create(managedRepository).GetAllAsync(CancellationToken.None);
            await Assert.That(firstDiscovery.Count).IsEqualTo(1);

            AddAdrCommitAndPush(seedRepositoryPath, remoteRepositoryPath, number: 2, slug: "use-queue", title: "Use Queue");
            var secondDiscovery = await factory.Create(managedRepository).GetAllAsync(CancellationToken.None);

            await Assert.That(secondDiscovery.Count).IsEqualTo(2);
            await Assert.That(secondDiscovery.Any(adr => adr.Number == 2)).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    private static void InitializeSeedAndRemote(string seedRepositoryPath, string remoteRepositoryPath)
    {
        Directory.CreateDirectory(seedRepositoryPath);
        Directory.CreateDirectory(remoteRepositoryPath);
        _ = Repository.Init(seedRepositoryPath);
        _ = Repository.Init(remoteRepositoryPath, isBare: true);
    }

    private static void AddAdrCommitAndPush(
        string seedRepositoryPath,
        string remoteRepositoryPath,
        int number,
        string slug,
        string title)
    {
        using var seedRepository = new Repository(seedRepositoryPath);
        if (seedRepository.Network.Remotes["origin"] is null)
        {
            _ = seedRepository.Network.Remotes.Add("origin", remoteRepositoryPath);
        }

        var adrFilePath = Path.Combine(seedRepositoryPath, "docs", "adr", $"adr-{number:0000}-{slug}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(adrFilePath)!);
        File.WriteAllText(
            adrFilePath,
            $$"""
            ---
            status: proposed
            date: 2026-03-20
            decision-makers: [Architecture Board]
            consulted: []
            informed: []
            ---
            # {{title}}

            ## Context and Problem Statement

            Seed content for {{title}}.
            """);

        var relativeAdrPath = Path.GetRelativePath(seedRepositoryPath, adrFilePath).Replace('\\', '/');
        Commands.Stage(seedRepository, relativeAdrPath);
        var signature = new Signature("ADR Portal Tests", "adr-portal-tests@example.com", DateTimeOffset.UtcNow);
        _ = seedRepository.Commit($"Add ADR {number:0000}", signature, signature);

        var branch = seedRepository.Head;
        seedRepository.Network.Push(
            seedRepository.Network.Remotes["origin"],
            $"{branch.CanonicalName}:{branch.CanonicalName}",
            new PushOptions());
    }

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "adr-phase-feedback-checkout-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                if (attempt == 10)
                {
                    return;
                }

                Thread.Sleep(50 * attempt);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 10)
                {
                    return;
                }

                Thread.Sleep(50 * attempt);
            }
        }
    }
}
