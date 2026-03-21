using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class GlobalLibraryServiceTests
{
    [Test]
    public async Task GetOverviewAsync_ProjectsCountsAndDashboardPending()
    {
        var globalIdA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var globalIdB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalIdA,
                    Title = "Use Vault",
                    CurrentVersion = 4,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-10),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new GlobalAdr
                {
                    GlobalId = globalIdB,
                    Title = "Use PostgreSQL",
                    CurrentVersion = 2,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-8),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            ],
            versions: [],
            proposals:
            [
                new GlobalAdrUpdateProposal
                {
                    GlobalId = globalIdA,
                    RepositoryId = 10,
                    LocalAdrNumber = 1,
                    ProposedFromVersion = 3,
                    ProposedTitle = "Use Vault with transit engine",
                    ProposedMarkdownContent = "# Use Vault",
                    IsPending = true,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
                },
                new GlobalAdrUpdateProposal
                {
                    GlobalId = globalIdA,
                    RepositoryId = 11,
                    LocalAdrNumber = 2,
                    ProposedFromVersion = 2,
                    ProposedTitle = "Historical review",
                    ProposedMarkdownContent = "# Use Vault",
                    IsPending = false,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-6)
                },
                new GlobalAdrUpdateProposal
                {
                    GlobalId = globalIdB,
                    RepositoryId = 12,
                    LocalAdrNumber = 5,
                    ProposedFromVersion = 1,
                    ProposedTitle = "Use PostgreSQL pooled connections",
                    ProposedMarkdownContent = "# Use PostgreSQL",
                    IsPending = true,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-3)
                }
            ],
            instances:
            [
                new GlobalAdrInstance
                {
                    GlobalId = globalIdA,
                    RepositoryId = 10,
                    LocalAdrNumber = 1,
                    RepoRelativePath = "docs/adr/adr-0001-use-vault.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 3,
                    HasLocalChanges = true,
                    UpdateAvailable = true,
                    LastReviewedAtUtc = DateTime.UtcNow.AddMinutes(-30)
                },
                new GlobalAdrInstance
                {
                    GlobalId = globalIdA,
                    RepositoryId = 11,
                    LocalAdrNumber = 2,
                    RepoRelativePath = "docs/adr/adr-0002-use-vault.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 4,
                    HasLocalChanges = false,
                    UpdateAvailable = false,
                    LastReviewedAtUtc = DateTime.UtcNow.AddMinutes(-40)
                },
                new GlobalAdrInstance
                {
                    GlobalId = globalIdB,
                    RepositoryId = 12,
                    LocalAdrNumber = 5,
                    RepoRelativePath = "docs/adr/adr-0005-use-postgresql.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 1,
                    HasLocalChanges = false,
                    UpdateAvailable = false,
                    LastReviewedAtUtc = DateTime.UtcNow.AddMinutes(-50)
                }
            ]);

        var service = CreateService(globalStore);

        var projection = await service.GetOverviewAsync(CancellationToken.None);

        await Assert.That(projection.Items.Count).IsEqualTo(2);
        await Assert.That(projection.DashboardPendingCount).IsEqualTo(3);
        await Assert.That(projection.Items[0].GlobalId).IsEqualTo(globalIdA);

        var vault = projection.Items.Single(item => item.GlobalId == globalIdA);
        await Assert.That(vault.CurrentVersion).IsEqualTo(4);
        await Assert.That(vault.InstanceCount).IsEqualTo(2);
        await Assert.That(vault.UpdateAvailableCount).IsEqualTo(1);
        await Assert.That(vault.PendingProposalCount).IsEqualTo(1);

        var postgres = projection.Items.Single(item => item.GlobalId == globalIdB);
        await Assert.That(postgres.CurrentVersion).IsEqualTo(2);
        await Assert.That(postgres.InstanceCount).IsEqualTo(1);
        await Assert.That(postgres.UpdateAvailableCount).IsEqualTo(0);
        await Assert.That(postgres.PendingProposalCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetDetailAsync_ProjectsCountsAndBaselineDiff()
    {
        var globalId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var repositoryA = CreateRepository(25, "contoso/repo-a");
        var repositoryB = CreateRepository(26, "contoso/repo-b");
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use OpenTelemetry",
                    CurrentVersion = 2,
                    RegisteredAtUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    LastUpdatedAtUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 1,
                    Title = "Use OpenTelemetry",
                    MarkdownContent = "# Use OpenTelemetry\nline-two",
                    CreatedAtUtc = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc)
                },
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 2,
                    Title = "Use OpenTelemetry for traces",
                    MarkdownContent = "# Use OpenTelemetry\nline-three",
                    CreatedAtUtc = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc)
                }
            ],
            proposals:
            [
                new GlobalAdrUpdateProposal
                {
                    GlobalId = globalId,
                    RepositoryId = repositoryA.Id,
                    LocalAdrNumber = 8,
                    ProposedFromVersion = 1,
                    ProposedTitle = "Use OpenTelemetry with semantic conventions",
                    ProposedMarkdownContent = "# Use OpenTelemetry",
                    IsPending = true,
                    CreatedAtUtc = new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc)
                },
                new GlobalAdrUpdateProposal
                {
                    GlobalId = globalId,
                    RepositoryId = repositoryB.Id,
                    LocalAdrNumber = 9,
                    ProposedFromVersion = 1,
                    ProposedTitle = "Historical proposal",
                    ProposedMarkdownContent = "# Use OpenTelemetry",
                    IsPending = false,
                    CreatedAtUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
                }
            ],
            instances:
            [
                new GlobalAdrInstance
                {
                    GlobalId = globalId,
                    RepositoryId = repositoryA.Id,
                    LocalAdrNumber = 8,
                    RepoRelativePath = "docs/adr/adr-0008-use-opentelemetry.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 1,
                    HasLocalChanges = true,
                    UpdateAvailable = true,
                    LastReviewedAtUtc = new DateTime(2026, 3, 3, 9, 0, 0, DateTimeKind.Utc)
                },
                new GlobalAdrInstance
                {
                    GlobalId = globalId,
                    RepositoryId = repositoryB.Id,
                    LocalAdrNumber = 9,
                    RepoRelativePath = "docs/adr/adr-0009-use-opentelemetry.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 2,
                    HasLocalChanges = false,
                    UpdateAvailable = false,
                    LastReviewedAtUtc = new DateTime(2026, 3, 3, 7, 0, 0, DateTimeKind.Utc)
                }
            ]);
        var managedStore = new FakeManagedRepositoryStore([repositoryA, repositoryB]);
        var service = CreateService(globalStore, managedStore);

        var detail = await service.GetDetailAsync(globalId, CancellationToken.None);

        await Assert.That(detail).IsNotNull();
        await Assert.That(detail!.GlobalId).IsEqualTo(globalId);
        await Assert.That(detail.CurrentVersion).IsEqualTo(2);
        await Assert.That(detail.Versions.Count).IsEqualTo(2);
        await Assert.That(detail.Versions[0].VersionNumber).IsEqualTo(2);
        await Assert.That(detail.PendingProposalCount).IsEqualTo(1);
        await Assert.That(detail.UpdateAvailableCount).IsEqualTo(1);
        await Assert.That(detail.BaselineDiff.Contains("Baseline diff: v1 -> v2", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detail.BaselineDiff.Contains("- line-two", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detail.BaselineDiff.Contains("+ line-three", StringComparison.Ordinal)).IsTrue();

        var pendingProposal = detail.Proposals.Single(proposal => proposal.IsPending);
        await Assert.That(pendingProposal.RepositoryDisplayName).IsEqualTo("contoso/repo-a");
    }

    [Test]
    public async Task ReconcileRepoAsync_UpsertsLinkedInstancesAndComputesFlags()
    {
        var repository = CreateRepository(40, "contoso/reconcile");
        var globalId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var unknownGlobalId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use Kafka",
                    CurrentVersion = 2,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-5),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 1,
                    Title = "Use Kafka",
                    MarkdownContent = "# Use Kafka\nbaseline",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-4)
                }
            ],
            proposals: [],
            instances: []);

        var adrs = new[]
        {
            CreateAdr(1, "Use Kafka", "use-kafka", AdrStatus.Accepted, globalId, 1, "# Use Kafka\nbaseline"),
            CreateAdr(2, "Use Kafka", "use-kafka", AdrStatus.Accepted, globalId, 1, "# Use Kafka\ncustomized"),
            CreateAdr(3, "Unlinked ADR", "unlinked-adr", AdrStatus.Proposed, null, null, "# Unlinked ADR"),
            CreateAdr(4, "Invalid version", "invalid-version", AdrStatus.Proposed, globalId, 0, "# Invalid version"),
            CreateAdr(5, "Unknown global", "unknown-global", AdrStatus.Proposed, unknownGlobalId, 1, "# Unknown global")
        };

        var managedStore = new FakeManagedRepositoryStore([repository]);
        var fileRepository = new FakeAdrFileRepository(adrs);
        var service = CreateService(globalStore, managedStore, fileRepository);

        var reconciledCount = await service.ReconcileRepoAsync(repository.Id, CancellationToken.None);

        await Assert.That(reconciledCount).IsEqualTo(2);
        await Assert.That(fileRepository.GetAllCallCount).IsEqualTo(1);

        var firstInstance = globalStore.Instances.Single(instance => instance.LocalAdrNumber == 1);
        await Assert.That(firstInstance.BaseTemplateVersion).IsEqualTo(1);
        await Assert.That(firstInstance.HasLocalChanges).IsFalse();
        await Assert.That(firstInstance.UpdateAvailable).IsTrue();

        var secondInstance = globalStore.Instances.Single(instance => instance.LocalAdrNumber == 2);
        await Assert.That(secondInstance.HasLocalChanges).IsTrue();
        await Assert.That(secondInstance.UpdateAvailable).IsTrue();
    }

    [Test]
    public async Task ProposeLibraryUpdateAsync_ThrowsWhenAdrHasNoLocalChanges()
    {
        var globalId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var repositoryId = 300;
        var repository = CreateRepository(repositoryId, "contoso/use-nats");
        var adr = CreateAdr(7, "Use NATS", "use-nats", AdrStatus.Accepted, globalId, 1, "# Use NATS\nbaseline");
        var fileRepository = new FakeAdrFileRepository([adr]);
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use NATS",
                    CurrentVersion = 2,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-3),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 1,
                    Title = "Use NATS",
                    MarkdownContent = "# Use NATS\nbaseline",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
                }
            ],
            proposals: [],
            instances: []);
        var service = CreateService(
            globalStore,
            managedStore: new FakeManagedRepositoryStore([repository]),
            fileRepository: fileRepository);

        Exception? exception = null;
        try
        {
            _ = await service.ProposeLibraryUpdateAsync(repositoryId, adr, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("no local changes", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(globalStore.Proposals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ProposeLibraryUpdateAsync_RegistersUnlinkedAdrAsNewGlobalTemplate()
    {
        var repositoryId = 302;
        var repository = CreateRepository(repositoryId, "contoso/unlinked-promotion");
        var unlinkedAdr = CreateAdr(
            number: 12,
            title: "Use Event Grid",
            slug: "use-event-grid",
            status: AdrStatus.Accepted,
            globalId: null,
            globalVersion: null,
            rawMarkdown: "# Use Event Grid");
        var fileRepository = new FakeAdrFileRepository([unlinkedAdr]);
        var globalStore = new FakeGlobalAdrStore(globalAdrs: [], versions: [], proposals: [], instances: []);
        var service = CreateService(
            globalStore,
            managedStore: new FakeManagedRepositoryStore([repository]),
            fileRepository: fileRepository);

        var result = await service.ProposeLibraryUpdateAsync(repositoryId, unlinkedAdr, CancellationToken.None);

        await Assert.That(result.Message.Contains("Promoted ADR-0012 to the global library as template v1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(globalStore.GlobalAdrs.Count).IsEqualTo(1);
        await Assert.That(globalStore.Versions.Count).IsEqualTo(1);
        await Assert.That(globalStore.Proposals.Count).IsEqualTo(0);
        await Assert.That(globalStore.Instances.Count).IsEqualTo(1);

        var createdGlobal = globalStore.GlobalAdrs.Single();
        await Assert.That(createdGlobal.Title).IsEqualTo("Use Event Grid");
        await Assert.That(createdGlobal.CurrentVersion).IsEqualTo(1);

        var createdVersion = globalStore.Versions.Single();
        await Assert.That(createdVersion.GlobalId).IsEqualTo(createdGlobal.GlobalId);
        await Assert.That(createdVersion.VersionNumber).IsEqualTo(1);
        await Assert.That(createdVersion.MarkdownContent).IsEqualTo("# Use Event Grid");

        var instance = globalStore.Instances.Single();
        await Assert.That(instance.GlobalId).IsEqualTo(createdGlobal.GlobalId);
        await Assert.That(instance.RepositoryId).IsEqualTo(repositoryId);
        await Assert.That(instance.LocalAdrNumber).IsEqualTo(12);
        await Assert.That(instance.BaseTemplateVersion).IsEqualTo(1);
        await Assert.That(instance.HasLocalChanges).IsFalse();
        await Assert.That(instance.UpdateAvailable).IsFalse();

        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.GlobalId).IsEqualTo(createdGlobal.GlobalId);
        await Assert.That(fileRepository.LastWrittenAdr!.GlobalVersion).IsEqualTo(1);
    }

    [Test]
    public async Task ProposeLibraryUpdateAsync_AddsPendingProposalWhenLocalChangesExist()
    {
        var globalId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var repositoryId = 301;
        var repository = CreateRepository(repositoryId, "contoso/use-redis");
        var adr = CreateAdr(8, "Use Redis", "use-redis", AdrStatus.Accepted, globalId, 1, "# Use Redis\ncustomized");
        var fileRepository = new FakeAdrFileRepository([adr]);
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use Redis",
                    CurrentVersion = 1,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-3),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 1,
                    Title = "Use Redis",
                    MarkdownContent = "# Use Redis\nbaseline",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
                }
            ],
            proposals: [],
            instances: []);
        var service = CreateService(
            globalStore,
            managedStore: new FakeManagedRepositoryStore([repository]),
            fileRepository: fileRepository);

        var result = await service.ProposeLibraryUpdateAsync(repositoryId, adr, CancellationToken.None);

        await Assert.That(result.Message.Contains("Created a pending library update proposal", StringComparison.Ordinal)).IsTrue();
        await Assert.That(globalStore.Proposals.Count).IsEqualTo(1);
        var proposal = globalStore.Proposals.Single();
        await Assert.That(proposal.IsPending).IsTrue();
        await Assert.That(proposal.RepositoryId).IsEqualTo(repositoryId);
        await Assert.That(proposal.LocalAdrNumber).IsEqualTo(8);
        await Assert.That(proposal.ProposedFromVersion).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyUpdateToInstanceAsync_ResetsFlagsAndMovesBaseVersion()
    {
        var globalId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var instanceId = 12;
        var repository = CreateRepository(500, "contoso/cosmos");
        var repositoryAdr = CreateAdr(
            number: 3,
            title: "Use Cosmos DB",
            slug: "use-cosmos-db",
            status: AdrStatus.Accepted,
            globalId: globalId,
            globalVersion: 2,
            rawMarkdown: "# Use Cosmos DB");
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use Cosmos DB",
                    CurrentVersion = 5,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-6),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 5,
                    Title = "Use Cosmos DB",
                    MarkdownContent = "# Use Cosmos DB\nUpdated template",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            proposals: [],
            instances:
            [
                new GlobalAdrInstance
                {
                    Id = instanceId,
                    GlobalId = globalId,
                    RepositoryId = 500,
                    LocalAdrNumber = 3,
                    RepoRelativePath = "docs/adr/adr-0003-use-cosmos-db.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 2,
                    HasLocalChanges = true,
                    UpdateAvailable = true,
                    LastReviewedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]);
        var service = CreateService(
            globalStore,
            managedStore: new FakeManagedRepositoryStore([repository]),
            fileRepository: new FakeAdrFileRepository([repositoryAdr]));

        var result = await service.ApplyUpdateToInstanceAsync(globalId, instanceId, CancellationToken.None);

        await Assert.That(result.Message.Contains("updated to v5", StringComparison.Ordinal)).IsTrue();
        var updated = globalStore.Instances.Single();
        await Assert.That(updated.BaseTemplateVersion).IsEqualTo(5);
        await Assert.That(updated.HasLocalChanges).IsFalse();
        await Assert.That(updated.UpdateAvailable).IsFalse();
    }

    [Test]
    public async Task CustomiseInstanceUpdateAsync_PreservesLocalChangesAndMovesBaseVersion()
    {
        var globalId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var instanceId = 13;
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use RabbitMQ",
                    CurrentVersion = 7,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-7),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            versions: [],
            proposals: [],
            instances:
            [
                new GlobalAdrInstance
                {
                    Id = instanceId,
                    GlobalId = globalId,
                    RepositoryId = 510,
                    LocalAdrNumber = 6,
                    RepoRelativePath = "docs/adr/adr-0006-use-rabbitmq.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 6,
                    HasLocalChanges = false,
                    UpdateAvailable = true,
                    LastReviewedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]);
        var service = CreateService(globalStore);

        var result = await service.CustomiseInstanceUpdateAsync(globalId, instanceId, CancellationToken.None);

        await Assert.That(result.Message.Contains("customised against v7", StringComparison.Ordinal)).IsTrue();
        var updated = globalStore.Instances.Single();
        await Assert.That(updated.BaseTemplateVersion).IsEqualTo(7);
        await Assert.That(updated.HasLocalChanges).IsTrue();
        await Assert.That(updated.UpdateAvailable).IsFalse();
    }

    [Test]
    public async Task DismissInstanceUpdateAsync_ClearsUpdateFlagWithoutChangingBaseVersion()
    {
        var globalId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var instanceId = 14;
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use OpenAPI",
                    CurrentVersion = 4,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-4),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ],
            versions: [],
            proposals: [],
            instances:
            [
                new GlobalAdrInstance
                {
                    Id = instanceId,
                    GlobalId = globalId,
                    RepositoryId = 520,
                    LocalAdrNumber = 10,
                    RepoRelativePath = "docs/adr/adr-0010-use-openapi.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 3,
                    HasLocalChanges = true,
                    UpdateAvailable = true,
                    LastReviewedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]);
        var service = CreateService(globalStore);

        var result = await service.DismissInstanceUpdateAsync(globalId, instanceId, CancellationToken.None);

        await Assert.That(result.Message.Contains("Dismissed update notification", StringComparison.Ordinal)).IsTrue();
        var updated = globalStore.Instances.Single();
        await Assert.That(updated.BaseTemplateVersion).IsEqualTo(3);
        await Assert.That(updated.HasLocalChanges).IsTrue();
        await Assert.That(updated.UpdateAvailable).IsFalse();
    }

    [Test]
    public async Task ApplyUpdateToInstanceAsync_QueuesGitPrWorkflowItem()
    {
        var globalId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var repository = CreateRepository(700, "contoso/queue-repo");
        var repositoryAdr = CreateAdr(
            number: 4,
            title: "Use HTTP APIs",
            slug: "use-http-apis",
            status: AdrStatus.Accepted,
            globalId: globalId,
            globalVersion: 1,
            rawMarkdown: "# Use HTTP APIs");
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use HTTP APIs",
                    CurrentVersion = 2,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-5),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddHours(-1)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 2,
                    Title = "Use HTTP APIs",
                    MarkdownContent = "# Use HTTP APIs\nUpdated",
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
                }
            ],
            proposals: [],
            instances:
            [
                new GlobalAdrInstance
                {
                    Id = 90,
                    GlobalId = globalId,
                    RepositoryId = repository.Id,
                    LocalAdrNumber = repositoryAdr.Number,
                    RepoRelativePath = repositoryAdr.RepoRelativePath,
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 1,
                    HasLocalChanges = true,
                    UpdateAvailable = true,
                    LastReviewedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]);
        var queue = new RecordingQueue();
        var service = CreateService(
            globalStore,
            managedStore: new FakeManagedRepositoryStore([repository]),
            fileRepository: new FakeAdrFileRepository([repositoryAdr]),
            queue: queue);

        _ = await service.ApplyUpdateToInstanceAsync(globalId, instanceId: 90, CancellationToken.None);

        await Assert.That(queue.Items.Count).IsEqualTo(1);
        var queued = queue.Items.Single();
        await Assert.That(queued.RepositoryId).IsEqualTo(repository.Id);
        await Assert.That(queued.Trigger).IsEqualTo(GitPrWorkflowTrigger.GlobalLibraryApply);
        await Assert.That(queued.Action).IsEqualTo(GitPrWorkflowAction.CreateOrUpdatePullRequest);
        await Assert.That(queued.AdrNumber).IsEqualTo(repositoryAdr.Number);
        await Assert.That(queued.RepoRelativePath).IsEqualTo(repositoryAdr.RepoRelativePath);
    }

    [Test]
    public async Task AcceptLibraryProposalAsync_PublishesVersionAndFlagsOtherInstances()
    {
        var globalId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sourceRepositoryId = 601;
        var sourceAdrNumber = 20;
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Use Dapr",
                    CurrentVersion = 3,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-20),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 3,
                    Title = "Use Dapr",
                    MarkdownContent = "# Use Dapr",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            ],
            proposals:
            [
                new GlobalAdrUpdateProposal
                {
                    Id = 44,
                    GlobalId = globalId,
                    RepositoryId = sourceRepositoryId,
                    LocalAdrNumber = sourceAdrNumber,
                    ProposedFromVersion = 3,
                    ProposedTitle = "Use Dapr sidecar resiliency",
                    ProposedMarkdownContent = "# Use Dapr sidecar resiliency",
                    IsPending = true,
                    CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
                }
            ],
            instances:
            [
                new GlobalAdrInstance
                {
                    Id = 70,
                    GlobalId = globalId,
                    RepositoryId = sourceRepositoryId,
                    LocalAdrNumber = sourceAdrNumber,
                    RepoRelativePath = "docs/adr/adr-0020-use-dapr.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 3,
                    HasLocalChanges = true,
                    UpdateAvailable = false,
                    LastReviewedAtUtc = DateTime.UtcNow.AddHours(-4)
                },
                new GlobalAdrInstance
                {
                    Id = 71,
                    GlobalId = globalId,
                    RepositoryId = 602,
                    LocalAdrNumber = 21,
                    RepoRelativePath = "docs/adr/adr-0021-use-dapr.md",
                    LastKnownStatus = AdrStatus.Accepted,
                    BaseTemplateVersion = 3,
                    HasLocalChanges = false,
                    UpdateAvailable = false,
                    LastReviewedAtUtc = DateTime.UtcNow.AddHours(-4)
                }
            ]);
        var service = CreateService(globalStore);

        var result = await service.AcceptLibraryProposalAsync(globalId, proposalId: 44, CancellationToken.None);

        await Assert.That(result.Message.Contains("published global version v4", StringComparison.Ordinal)).IsTrue();
        var updatedGlobal = globalStore.GlobalAdrs.Single();
        await Assert.That(updatedGlobal.CurrentVersion).IsEqualTo(4);
        await Assert.That(updatedGlobal.Title).IsEqualTo("Use Dapr sidecar resiliency");

        var acceptedProposal = globalStore.Proposals.Single(proposal => proposal.Id == 44);
        await Assert.That(acceptedProposal.IsPending).IsFalse();

        await Assert.That(globalStore.Versions.Any(version => version.GlobalId == globalId && version.VersionNumber == 4)).IsTrue();

        var sourceInstance = globalStore.Instances.Single(instance => instance.RepositoryId == sourceRepositoryId && instance.LocalAdrNumber == sourceAdrNumber);
        await Assert.That(sourceInstance.UpdateAvailable).IsFalse();
        var otherInstance = globalStore.Instances.Single(instance => instance.RepositoryId == 602);
        await Assert.That(otherInstance.UpdateAvailable).IsTrue();
    }

    private static GlobalLibraryService CreateService(
        FakeGlobalAdrStore globalStore,
        FakeManagedRepositoryStore? managedStore = null,
        FakeAdrFileRepository? fileRepository = null,
        RecordingQueue? queue = null)
    {
        var resolvedManagedStore = managedStore ?? new FakeManagedRepositoryStore([]);
        var resolvedRepository = fileRepository ?? new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(resolvedRepository);
        return new GlobalLibraryService(globalStore, resolvedManagedStore, factory, queue, gitPrWorkflowProcessor: null);
    }

    private static ManagedRepository CreateRepository(int id, string displayName)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = displayName,
            RootPath = $@"C:\repos\{displayName.Replace('/', '-')}",
            AdrFolder = "docs/adr",
            GitRemoteUrl = $"https://github.com/{displayName}.git",
            IsActive = true
        };
    }

    private static Adr CreateAdr(
        int number,
        string title,
        string slug,
        AdrStatus status,
        Guid? globalId,
        int? globalVersion,
        string rawMarkdown)
    {
        return new Adr
        {
            Number = number,
            Title = title,
            Slug = slug,
            Status = status,
            Date = new DateOnly(2026, 3, 19),
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            GlobalId = globalId,
            GlobalVersion = globalVersion,
            RawMarkdown = rawMarkdown
        };
    }

    private sealed class FakeAdrFileRepository(IReadOnlyList<Adr> adrs) : IAdrFileRepository
    {
        private readonly Dictionary<int, Adr> adrsByNumber = adrs.ToDictionary(item => item.Number);

        public int GetAllCallCount { get; private set; }
        public Adr? LastWrittenAdr { get; private set; }

        public Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetAllCallCount++;
            return Task.FromResult<IReadOnlyList<Adr>>(adrsByNumber.Values.OrderBy(item => item.Number).ToArray());
        }

        public Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = adrsByNumber.TryGetValue(number, out var adr);
            return Task.FromResult(adr);
        }

        public Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            adrsByNumber[adr.Number] = adr;
            LastWrittenAdr = adr;
            return Task.FromResult(adr);
        }

        public Task MoveToRejectedAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<int> GetNextNumberAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(adrsByNumber.Count is 0 ? 1 : adrsByNumber.Keys.Max() + 1);
        }
    }

    private sealed class FakeMadrRepositoryFactory(FakeAdrFileRepository repository) : IMadrRepositoryFactory
    {
        public IAdrFileRepository Create(ManagedRepository managedRepository)
        {
            ArgumentNullException.ThrowIfNull(managedRepository);
            return repository;
        }
    }

    private sealed class RecordingQueue : IGitPrWorkflowQueue
    {
        public List<GitPrWorkflowQueueItem> Items { get; } = [];

        public Task EnqueueAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(item);
            ct.ThrowIfCancellationRequested();
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpsertAsync(GitPrWorkflowQueueItem item, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(item);
            ct.ThrowIfCancellationRequested();
            var index = Items.FindIndex(existing => existing.Id == item.Id);
            if (index >= 0)
            {
                Items[index] = item;
            }
            else
            {
                Items.Add(item);
            }

            return Task.CompletedTask;
        }

        public Task<GitPrWorkflowQueueItem?> GetLatestForAdrAsync(int repositoryId, int adrNumber, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var item = Items
                .Where(existing => existing.RepositoryId == repositoryId && existing.AdrNumber == adrNumber)
                .OrderByDescending(existing => existing.EnqueuedAtUtc)
                .ThenByDescending(existing => existing.Id)
                .FirstOrDefault();
            return Task.FromResult(item);
        }

        public Task<GitPrWorkflowQueueItem?> GetByIdAsync(Guid queueItemId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Items.FirstOrDefault(existing => existing.Id == queueItemId));
        }
    }

    private sealed class FakeManagedRepositoryStore(IEnumerable<ManagedRepository> repositories) : IManagedRepositoryStore
    {
        private readonly Dictionary<int, ManagedRepository> repositoriesById = repositories.ToDictionary(repository => repository.Id);

        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = repositoriesById.TryGetValue(repositoryId, out var repository);
            return Task.FromResult(repository);
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ManagedRepository>>(repositoriesById.Values.OrderBy(repository => repository.Id).ToArray());
        }

        public Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repositoryIds);
            ct.ThrowIfCancellationRequested();
            var dictionary = repositoriesById
                .Where(pair => repositoryIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(dictionary);
        }

        public Task<ManagedRepository> AddAsync(ManagedRepository repository, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ct.ThrowIfCancellationRequested();
            repositoriesById[repository.Id] = repository;
            return Task.FromResult(repository);
        }

        public Task<ManagedRepository?> UpdateAsync(ManagedRepository repository, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ct.ThrowIfCancellationRequested();
            if (!repositoriesById.ContainsKey(repository.Id))
            {
                return Task.FromResult<ManagedRepository?>(null);
            }

            repositoriesById[repository.Id] = repository;
            return Task.FromResult<ManagedRepository?>(repository);
        }

        public Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repositoriesById.Remove(repositoryId));
        }
    }

    private sealed class FakeGlobalAdrStore : IGlobalAdrStore
    {
        private readonly Dictionary<Guid, GlobalAdr> globalAdrsById;
        private readonly List<GlobalAdrVersion> versions;
        private readonly List<GlobalAdrUpdateProposal> proposals;
        private readonly Dictionary<(Guid GlobalId, int RepositoryId, int LocalAdrNumber), GlobalAdrInstance> instancesByKey;

        public FakeGlobalAdrStore(
            IReadOnlyList<GlobalAdr> globalAdrs,
            IReadOnlyList<GlobalAdrVersion> versions,
            IReadOnlyList<GlobalAdrUpdateProposal> proposals,
            IReadOnlyList<GlobalAdrInstance> instances)
        {
            globalAdrsById = globalAdrs.ToDictionary(item => item.GlobalId);
            this.versions = [.. versions];
            this.proposals = [.. proposals];
            instancesByKey = instances.ToDictionary(item => (item.GlobalId, item.RepositoryId, item.LocalAdrNumber));

            var nextVersionId = 1;
            foreach (var version in this.versions)
            {
                if (version.Id <= 0)
                {
                    version.Id = nextVersionId;
                }

                nextVersionId = Math.Max(nextVersionId, version.Id + 1);
            }

            var nextProposalId = 1;
            foreach (var proposal in this.proposals)
            {
                if (proposal.Id <= 0)
                {
                    proposal.Id = nextProposalId;
                }

                nextProposalId = Math.Max(nextProposalId, proposal.Id + 1);
            }

            var nextInstanceId = 1;
            foreach (var instance in instancesByKey.Values)
            {
                if (instance.Id <= 0)
                {
                    instance.Id = nextInstanceId;
                }

                nextInstanceId = Math.Max(nextInstanceId, instance.Id + 1);
            }
        }

        public IReadOnlyList<GlobalAdr> GlobalAdrs => globalAdrsById.Values.OrderBy(item => item.GlobalId).ToArray();
        public IReadOnlyList<GlobalAdrVersion> Versions => versions;
        public IReadOnlyList<GlobalAdrUpdateProposal> Proposals => proposals;
        public IReadOnlyList<GlobalAdrInstance> Instances => instancesByKey.Values.ToArray();

        public Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = globalAdrsById.TryGetValue(globalId, out var globalAdr);
            return Task.FromResult(globalAdr);
        }

        public Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdr>>(globalAdrsById.Values.OrderBy(item => item.Title).ToArray());
        }

        public Task<IReadOnlyList<GlobalAdrVersion>> GetVersionsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrVersion>>(
                versions
                    .Where(version => version.GlobalId == globalId)
                    .OrderByDescending(version => version.VersionNumber)
                    .ToArray());
        }

        public Task<IReadOnlyList<GlobalAdrUpdateProposal>> GetUpdateProposalsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrUpdateProposal>>(
                proposals
                    .Where(proposal => proposal.GlobalId == globalId)
                    .OrderByDescending(proposal => proposal.IsPending)
                    .ThenByDescending(proposal => proposal.CreatedAtUtc)
                    .ToArray());
        }

        public Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrInstance>>(
                instancesByKey.Values
                    .Where(instance => instance.GlobalId == globalId)
                    .OrderBy(instance => instance.RepositoryId)
                    .ThenBy(instance => instance.LocalAdrNumber)
                    .ToArray());
        }

        public Task<int> GetDashboardPendingCountAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var pendingProposalCount = proposals.Count(proposal => proposal.IsPending);
            var updateAvailableCount = instancesByKey.Values.Count(instance => instance.UpdateAvailable);
            return Task.FromResult(pendingProposalCount + updateAvailableCount);
        }

        public Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(globalAdr);
            ct.ThrowIfCancellationRequested();
            globalAdrsById[globalAdr.GlobalId] = globalAdr;
            return Task.FromResult(globalAdr);
        }

        public Task<GlobalAdr?> UpdateAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(globalAdr);
            ct.ThrowIfCancellationRequested();
            if (!globalAdrsById.ContainsKey(globalAdr.GlobalId))
            {
                return Task.FromResult<GlobalAdr?>(null);
            }

            globalAdrsById[globalAdr.GlobalId] = globalAdr;
            return Task.FromResult<GlobalAdr?>(globalAdr);
        }

        public Task<GlobalAdrVersion> AddVersionAsync(GlobalAdrVersion version, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(version);
            ct.ThrowIfCancellationRequested();
            if (version.Id <= 0)
            {
                version.Id = versions.Count is 0 ? 1 : versions.Max(item => item.Id) + 1;
            }

            versions.Add(version);
            return Task.FromResult(version);
        }

        public Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();
            if (proposal.Id <= 0)
            {
                proposal.Id = proposals.Count is 0 ? 1 : proposals.Max(item => item.Id) + 1;
            }

            proposals.Add(proposal);
            return Task.FromResult(proposal);
        }

        public Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();
            var index = proposals.FindIndex(item => item.Id == proposal.Id && item.GlobalId == proposal.GlobalId);
            if (index < 0)
            {
                return Task.FromResult<GlobalAdrUpdateProposal?>(null);
            }

            proposals[index] = proposal;
            return Task.FromResult<GlobalAdrUpdateProposal?>(proposal);
        }

        public Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ct.ThrowIfCancellationRequested();
            var key = (instance.GlobalId, instance.RepositoryId, instance.LocalAdrNumber);
            if (instancesByKey.TryGetValue(key, out var existing))
            {
                existing.RepoRelativePath = instance.RepoRelativePath;
                existing.LastKnownStatus = instance.LastKnownStatus;
                existing.BaseTemplateVersion = instance.BaseTemplateVersion;
                existing.HasLocalChanges = instance.HasLocalChanges;
                existing.UpdateAvailable = instance.UpdateAvailable;
                existing.LastReviewedAtUtc = instance.LastReviewedAtUtc;
                return Task.FromResult(existing);
            }

            if (instance.Id <= 0)
            {
                instance.Id = instancesByKey.Count is 0 ? 1 : instancesByKey.Values.Max(item => item.Id) + 1;
            }

            instancesByKey[key] = instance;
            return Task.FromResult(instance);
        }
    }
}
