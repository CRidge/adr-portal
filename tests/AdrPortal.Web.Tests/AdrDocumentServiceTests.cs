using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.Components.Pages;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrDocumentServiceTests
{
    [Test]
    public async Task GetRepositoryWithAdrsAsync_ReturnsRepositoryAndList_WhenRepositoryExists()
    {
        var repository = CreateRepository(id: 42);
        var adrs = new[]
        {
            CreateAdr(number: 1, title: "Use PostgreSQL", slug: "use-postgresql", status: AdrStatus.Accepted),
            CreateAdr(number: 2, title: "Introduce API Gateway Caching", slug: "introduce-api-gateway-caching", status: AdrStatus.Proposed)
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(adrs);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.GetRepositoryWithAdrsAsync(repository.Id, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Repository.Id).IsEqualTo(42);
        await Assert.That(result.Value.Adrs.Count).IsEqualTo(2);
        await Assert.That(result.Value.Adrs[0].Number).IsEqualTo(1);
        await Assert.That(factory.LastRepositoryId).IsEqualTo(42);
        await Assert.That(fileRepository.GetAllCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetRepositoryWithAdrsAsync_ReturnsNull_WhenRepositoryMissing()
    {
        var managedStore = new FakeManagedRepositoryStore(repository: null);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.GetRepositoryWithAdrsAsync(repositoryId: 999, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(factory.LastRepositoryId).IsNull();
        await Assert.That(fileRepository.GetAllCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetRepositoryAdrAsync_ReturnsAdr_WhenNumberExists()
    {
        var repository = CreateRepository(id: 7);
        var adrs = new[]
        {
            CreateAdr(number: 12, title: "Deprecate Legacy RPC", slug: "deprecate-legacy-rpc", status: AdrStatus.Deprecated)
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(adrs);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.GetRepositoryAdrAsync(repositoryId: 7, number: 12, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Repository.Id).IsEqualTo(7);
        await Assert.That(result.Value.Adr.Number).IsEqualTo(12);
        await Assert.That(fileRepository.GetByNumberCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetRepositoryAdrAsync_ReturnsNull_WhenAdrMissing()
    {
        var repository = CreateRepository(id: 8);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.GetRepositoryAdrAsync(repositoryId: 8, number: 404, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(fileRepository.GetByNumberCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetRepositoryForCreateAsync_ReturnsNextNumber_WhenRepositoryExists()
    {
        var repository = CreateRepository(id: 13);
        var fileRepository = new FakeAdrFileRepository([
            CreateAdr(number: 4, title: "Use PostgreSQL", slug: "use-postgresql", status: AdrStatus.Accepted),
            CreateAdr(number: 9, title: "Use Redis Cache", slug: "use-redis-cache", status: AdrStatus.Proposed)
        ]);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.GetRepositoryForCreateAsync(repository.Id, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.NextNumber).IsEqualTo(10);
        await Assert.That(fileRepository.GetNextNumberCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task CreateAdrAsync_PersistsMarkdownUsingRepositoryWrite()
    {
        var repository = CreateRepository(id: 21);
        var fileRepository = new FakeAdrFileRepository([
            CreateAdr(number: 1, title: "Use PostgreSQL", slug: "use-postgresql", status: AdrStatus.Accepted)
        ]);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);
        var editorModel = AdrEditorModel.CreateForNew();
        editorModel.Title = "Introduce Request Caching";
        editorModel.Slug = "introduce-request-caching";
        editorModel.Status = AdrStatus.Proposed;
        editorModel.Date = new DateOnly(2026, 3, 19);
        editorModel.DecisionMakersText = "Architecture Board";
        editorModel.ConsultedText = "Security";
        editorModel.InformedText = "Developers";
        editorModel.MarkdownBody = """
## Context and Problem Statement

Latency is too high.
""";

        var result = await service.CreateAdrAsync(repository.Id, editorModel.ToInput(), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Repository.Id).IsEqualTo(repository.Id);
        await Assert.That(result.Adr.Number).IsEqualTo(2);
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(1);
        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.RepoRelativePath).IsEqualTo("docs/adr/adr-0002-introduce-request-caching.md");
        await Assert.That(fileRepository.LastWrittenAdr.RawMarkdown.StartsWith("# Introduce Request Caching", StringComparison.Ordinal)).IsTrue();
        await Assert.That(fileRepository.LastWrittenAdr.DecisionMakers.Count).IsEqualTo(1);
        await Assert.That(fileRepository.LastWrittenAdr.DecisionMakers[0]).IsEqualTo("Architecture Board");
    }

    [Test]
    public async Task UpdateAdrAsync_PersistsUpdatedAdr_WhenAdrExists()
    {
        var repository = CreateRepository(id: 31);
        var existingAdr = CreateAdr(number: 8, title: "Use Redis", slug: "use-redis", status: AdrStatus.Proposed) with
        {
            GlobalId = Guid.Parse("4f0f9d71-f1f5-414b-8dc7-90bd61ab0cba"),
            GlobalVersion = 3
        };

        var fileRepository = new FakeAdrFileRepository([existingAdr]);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore([
            new GlobalAdr
            {
                GlobalId = existingAdr.GlobalId!.Value,
                Title = "Use Redis",
                CurrentVersion = 3,
                RegisteredAtUtc = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc),
                LastUpdatedAtUtc = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
            }
        ]);
        var service = new AdrDocumentService(managedStore, factory, globalStore);
        var updateModel = AdrEditorModel.FromAdr(existingAdr);
        updateModel.Title = "Use Redis for Session Cache";
        updateModel.Slug = "use-redis-session-cache";
        updateModel.Status = AdrStatus.Accepted;
        updateModel.DecisionMakersText = "Architecture Board";
        updateModel.MarkdownBody = """
# Use Redis for Session Cache

## Context and Problem Statement

Updated decision details.
""";

        var result = await service.UpdateAdrAsync(repository.Id, existingAdr.Number, updateModel.ToInput(), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.Number).IsEqualTo(8);
        await Assert.That(fileRepository.GetByNumberCallCount).IsEqualTo(1);
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(1);
        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.Title).IsEqualTo("Use Redis for Session Cache");
        await Assert.That(fileRepository.LastWrittenAdr.Status).IsEqualTo(AdrStatus.Accepted);
        await Assert.That(fileRepository.LastWrittenAdr.GlobalId).IsEqualTo(existingAdr.GlobalId);
        await Assert.That(fileRepository.LastWrittenAdr.GlobalVersion).IsEqualTo(existingAdr.GlobalVersion);
    }

    [Test]
    public async Task CreateAdrAsync_ThrowsArgumentException_WhenDecisionMakersMissing()
    {
        var repository = CreateRepository(id: 55);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);
        var input = new AdrEditorInput
        {
            Title = "Use Queues",
            Slug = "use-queues",
            Status = AdrStatus.Proposed,
            Date = new DateOnly(2026, 3, 20),
            DecisionMakers = [],
            Consulted = [],
            Informed = [],
            BodyMarkdown = "## Context and Problem Statement"
        };

        Exception? exception = null;
        try
        {
            _ = await service.CreateAdrAsync(repository.Id, input, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
        await Assert.That(exception!.Message.Contains("decision maker", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task CreateAdrAsync_ThrowsArgumentException_WhenRawHtmlIsIncluded()
    {
        var repository = CreateRepository(id: 56);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);
        var input = new AdrEditorInput
        {
            Title = "Use Queues",
            Slug = "use-queues",
            Status = AdrStatus.Proposed,
            Date = new DateOnly(2026, 3, 20),
            DecisionMakers = ["Board"],
            Consulted = [],
            Informed = [],
            BodyMarkdown = "## Context\n\n<script>alert('xss')</script>"
        };

        Exception? exception = null;
        try
        {
            _ = await service.CreateAdrAsync(repository.Id, input, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
        await Assert.That(exception!.Message.Contains("raw html", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task UpdateAdrAsync_ReturnsNull_WhenAdrDoesNotExist()
    {
        var repository = CreateRepository(id: 77);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);
        var input = new AdrEditorInput
        {
            Title = "Use Queues",
            Slug = "use-queues",
            Status = AdrStatus.Proposed,
            Date = new DateOnly(2026, 3, 20),
            DecisionMakers = ["Board"],
            Consulted = [],
            Informed = [],
            BodyMarkdown = "## Context and Problem Statement"
        };

        var result = await service.UpdateAdrAsync(repository.Id, number: 100, input, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task TransitionAdrAsync_Accept_RegistersUnlinkedAdrAsGlobalVersionOne()
    {
        var repository = CreateRepository(id: 90);
        var proposedAdr = CreateAdr(number: 14, title: "Adopt OpenTelemetry", slug: "adopt-opentelemetry", status: AdrStatus.Proposed);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([proposedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.TransitionAdrAsync(repository.Id, proposedAdr.Number, AdrTransitionAction.Accept, supersededByNumber: null, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.Status).IsEqualTo(AdrStatus.Accepted);
        await Assert.That(result.Adr.GlobalId).IsNotNull();
        await Assert.That(result.Adr.GlobalVersion).IsEqualTo(1);
        await Assert.That(result.Adr.SupersededByNumber).IsNull();
        await Assert.That(globalStore.AddCallCount).IsEqualTo(1);
        await Assert.That(globalStore.UpsertCallCount).IsEqualTo(1);
        await Assert.That(globalStore.LastAddedGlobalAdr).IsNotNull();
        await Assert.That(globalStore.LastAddedGlobalAdr!.CurrentVersion).IsEqualTo(1);
        await Assert.That(globalStore.LastUpsertedInstance).IsNotNull();
        await Assert.That(globalStore.LastUpsertedInstance!.BaseTemplateVersion).IsEqualTo(1);
        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.GlobalId).IsEqualTo(result.Adr.GlobalId);
        await Assert.That(fileRepository.LastWrittenAdr.GlobalVersion).IsEqualTo(1);
        await Assert.That(result.Message.Contains("registered in the global library as v1", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task TransitionAdrAsync_Accept_PreservesExistingGlobalLinkAndOnlyUpsertsInstance()
    {
        var repository = CreateRepository(id: 91);
        var globalId = Guid.Parse("9ad4d713-0f2e-4564-90fe-a7980ed10d32");
        var proposedAdr = CreateAdr(number: 4, title: "Use Vault", slug: "use-vault", status: AdrStatus.Proposed) with
        {
            GlobalId = globalId,
            GlobalVersion = 3
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([proposedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore([
            new GlobalAdr
            {
                GlobalId = globalId,
                Title = "Use Vault",
                CurrentVersion = 5,
                RegisteredAtUtc = DateTime.UtcNow.AddDays(-5),
                LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        ]);
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.TransitionAdrAsync(repository.Id, proposedAdr.Number, AdrTransitionAction.Accept, supersededByNumber: null, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.Status).IsEqualTo(AdrStatus.Accepted);
        await Assert.That(result.Adr.GlobalId).IsEqualTo(globalId);
        await Assert.That(result.Adr.GlobalVersion).IsEqualTo(3);
        await Assert.That(globalStore.AddCallCount).IsEqualTo(0);
        await Assert.That(globalStore.UpsertCallCount).IsEqualTo(1);
        await Assert.That(globalStore.LastUpsertedInstance).IsNotNull();
        await Assert.That(globalStore.LastUpsertedInstance!.GlobalId).IsEqualTo(globalId);
        await Assert.That(globalStore.LastUpsertedInstance.BaseTemplateVersion).IsEqualTo(3);
        await Assert.That(result.Message.Contains("linked to existing global ADR", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task TransitionAdrAsync_Accept_UsesGlobalCurrentVersionWhenAdrVersionMissing()
    {
        var repository = CreateRepository(id: 92);
        var globalId = Guid.Parse("6846a939-2840-40ce-8321-a0cd2d683178");
        var proposedAdr = CreateAdr(number: 11, title: "Use Kafka", slug: "use-kafka", status: AdrStatus.Proposed) with
        {
            GlobalId = globalId,
            GlobalVersion = null
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([proposedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore([
            new GlobalAdr
            {
                GlobalId = globalId,
                Title = "Use Kafka",
                CurrentVersion = 6,
                RegisteredAtUtc = DateTime.UtcNow.AddDays(-10),
                LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        ]);
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.TransitionAdrAsync(repository.Id, proposedAdr.Number, AdrTransitionAction.Accept, supersededByNumber: null, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.GlobalVersion).IsEqualTo(6);
        await Assert.That(globalStore.LastUpsertedInstance).IsNotNull();
        await Assert.That(globalStore.LastUpsertedInstance!.BaseTemplateVersion).IsEqualTo(6);
    }

    [Test]
    public async Task TransitionAdrAsync_Accept_ThrowsWhenLinkedGlobalAdrIsMissing()
    {
        var repository = CreateRepository(id: 93);
        var missingGlobalId = Guid.Parse("581e042f-7fa9-4052-b157-07a2a3f56f4d");
        var proposedAdr = CreateAdr(number: 16, title: "Use NATS", slug: "use-nats", status: AdrStatus.Proposed) with
        {
            GlobalId = missingGlobalId,
            GlobalVersion = 2
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([proposedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        Exception? exception = null;
        try
        {
            _ = await service.TransitionAdrAsync(repository.Id, proposedAdr.Number, AdrTransitionAction.Accept, supersededByNumber: null, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("not registered", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(0);
        await Assert.That(globalStore.UpsertCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task TransitionAdrAsync_Reject_FromProposed_UpdatesStatus()
    {
        var repository = CreateRepository(id: 94);
        var proposedAdr = CreateAdr(number: 22, title: "Use SOAP", slug: "use-soap", status: AdrStatus.Proposed) with
        {
            SupersededByNumber = 100
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([proposedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.TransitionAdrAsync(repository.Id, proposedAdr.Number, AdrTransitionAction.Reject, supersededByNumber: null, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.Status).IsEqualTo(AdrStatus.Rejected);
        await Assert.That(result.Adr.SupersededByNumber).IsNull();
        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.Status).IsEqualTo(AdrStatus.Rejected);
    }

    [Test]
    public async Task TransitionAdrAsync_Reject_FromAccepted_Throws()
    {
        var repository = CreateRepository(id: 95);
        var acceptedAdr = CreateAdr(number: 6, title: "Use gRPC", slug: "use-grpc", status: AdrStatus.Accepted);

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([acceptedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        Exception? exception = null;
        try
        {
            _ = await service.TransitionAdrAsync(repository.Id, acceptedAdr.Number, AdrTransitionAction.Reject, supersededByNumber: null, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("proposed", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task TransitionAdrAsync_Supersede_FromAccepted_UpdatesStatusAndSupersededBy()
    {
        var repository = CreateRepository(id: 96);
        var acceptedAdr = CreateAdr(number: 18, title: "Use Elastic", slug: "use-elastic", status: AdrStatus.Accepted);

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([acceptedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.TransitionAdrAsync(repository.Id, acceptedAdr.Number, AdrTransitionAction.Supersede, supersededByNumber: 25, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.Status).IsEqualTo(AdrStatus.Superseded);
        await Assert.That(result.Adr.SupersededByNumber).IsEqualTo(25);
        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.Status).IsEqualTo(AdrStatus.Superseded);
        await Assert.That(fileRepository.LastWrittenAdr.SupersededByNumber).IsEqualTo(25);
    }

    [Test]
    public async Task TransitionAdrAsync_Supersede_RequiresPositiveTargetAdrNumber()
    {
        var repository = CreateRepository(id: 97);
        var acceptedAdr = CreateAdr(number: 19, title: "Use RabbitMQ", slug: "use-rabbitmq", status: AdrStatus.Accepted);

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([acceptedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        Exception? exception = null;
        try
        {
            _ = await service.TransitionAdrAsync(repository.Id, acceptedAdr.Number, AdrTransitionAction.Supersede, supersededByNumber: 0, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
        await Assert.That(exception!.Message.Contains("greater than zero", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(fileRepository.WriteCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task TransitionAdrAsync_Supersede_RejectsSelfReference()
    {
        var repository = CreateRepository(id: 98);
        var acceptedAdr = CreateAdr(number: 20, title: "Use SQL Server", slug: "use-sql-server", status: AdrStatus.Accepted);

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([acceptedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        Exception? exception = null;
        try
        {
            _ = await service.TransitionAdrAsync(repository.Id, acceptedAdr.Number, AdrTransitionAction.Supersede, supersededByNumber: acceptedAdr.Number, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
        await Assert.That(exception!.Message.Contains("different", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task TransitionAdrAsync_Deprecate_FromAccepted_UpdatesStatus()
    {
        var repository = CreateRepository(id: 99);
        var acceptedAdr = CreateAdr(number: 21, title: "Use XML", slug: "use-xml", status: AdrStatus.Accepted) with
        {
            SupersededByNumber = 29
        };

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([acceptedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var result = await service.TransitionAdrAsync(repository.Id, acceptedAdr.Number, AdrTransitionAction.Deprecate, supersededByNumber: null, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Adr.Status).IsEqualTo(AdrStatus.Deprecated);
        await Assert.That(result.Adr.SupersededByNumber).IsNull();
        await Assert.That(fileRepository.LastWrittenAdr).IsNotNull();
        await Assert.That(fileRepository.LastWrittenAdr!.Status).IsEqualTo(AdrStatus.Deprecated);
    }

    [Test]
    public async Task TransitionAdrAsync_Deprecate_FromProposed_Throws()
    {
        var repository = CreateRepository(id: 100);
        var proposedAdr = CreateAdr(number: 23, title: "Use FTP", slug: "use-ftp", status: AdrStatus.Proposed);

        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([proposedAdr]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        Exception? exception = null;
        try
        {
            _ = await service.TransitionAdrAsync(repository.Id, proposedAdr.Number, AdrTransitionAction.Deprecate, supersededByNumber: null, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("accepted", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task TransitionAdrAsync_ReturnsNull_WhenRepositoryOrAdrMissing()
    {
        var repository = CreateRepository(id: 101);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository(Array.Empty<Adr>());
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var globalStore = new FakeGlobalAdrStore();
        var service = new AdrDocumentService(managedStore, factory, globalStore);

        var missingAdrResult = await service.TransitionAdrAsync(repository.Id, number: 1, AdrTransitionAction.Accept, supersededByNumber: null, CancellationToken.None);
        var missingRepositoryResult = await service.TransitionAdrAsync(repositoryId: 9999, number: 1, AdrTransitionAction.Accept, supersededByNumber: null, CancellationToken.None);

        await Assert.That(missingAdrResult).IsNull();
        await Assert.That(missingRepositoryResult).IsNull();
    }

    private static ManagedRepository CreateRepository(int id)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = "contoso/adr-portal",
            RootPath = @"C:\repos\contoso\adr-portal",
            AdrFolder = "docs/adr",
            IsActive = true
        };
    }

    private static Adr CreateAdr(int number, string title, string slug, AdrStatus status)
    {
        return new Adr
        {
            Number = number,
            Title = title,
            Slug = slug,
            Status = status,
            Date = new DateOnly(2026, 3, 19),
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            RawMarkdown = $"# {title}"
        };
    }

    private sealed class FakeManagedRepositoryStore(ManagedRepository? repository) : IManagedRepositoryStore
    {
        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (repository is null || repository.Id != repositoryId)
            {
                return Task.FromResult<ManagedRepository?>(null);
            }

            return Task.FromResult<ManagedRepository?>(repository);
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<ManagedRepository> repositories = repository is null ? [] : [repository];
            return Task.FromResult(repositories);
        }

        public Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repositoryIds);
            ct.ThrowIfCancellationRequested();

            if (repository is null)
            {
                return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(new Dictionary<int, ManagedRepository>());
            }

            if (!repositoryIds.Contains(repository.Id))
            {
                return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(new Dictionary<int, ManagedRepository>());
            }

            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(
                new Dictionary<int, ManagedRepository> { [repository.Id] = repository });
        }

        public Task<ManagedRepository> AddAsync(ManagedRepository addRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(addRepository);
        }

        public Task<ManagedRepository?> UpdateAsync(ManagedRepository updateRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<ManagedRepository?>(updateRepository);
        }

        public Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMadrRepositoryFactory(FakeAdrFileRepository repository) : IMadrRepositoryFactory
    {
        public int? LastRepositoryId { get; private set; }

        public IAdrFileRepository Create(ManagedRepository managedRepository)
        {
            LastRepositoryId = managedRepository.Id;
            return repository;
        }
    }

    private sealed class FakeAdrFileRepository(IReadOnlyList<Adr> adrs) : IAdrFileRepository
    {
        public int GetAllCallCount { get; private set; }
        public int GetByNumberCallCount { get; private set; }
        public int GetNextNumberCallCount { get; private set; }
        public int WriteCallCount { get; private set; }
        public Adr? LastWrittenAdr { get; private set; }
        private readonly List<Adr> store = [.. adrs];

        public Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetAllCallCount++;
            return Task.FromResult<IReadOnlyList<Adr>>(store);
        }

        public Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            GetByNumberCallCount++;
            var adr = store.SingleOrDefault(item => item.Number == number);
            return Task.FromResult(adr);
        }

        public Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            WriteCallCount++;
            LastWrittenAdr = adr;

            var existingIndex = store.FindIndex(item => item.Number == adr.Number);
            if (existingIndex >= 0)
            {
                store[existingIndex] = adr;
            }
            else
            {
                store.Add(adr);
            }

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
            GetNextNumberCallCount++;
            var nextNumber = store.Count is 0 ? 1 : store.Max(item => item.Number) + 1;
            return Task.FromResult(nextNumber);
        }
    }

    private sealed class FakeGlobalAdrStore : IGlobalAdrStore
    {
        private readonly Dictionary<Guid, GlobalAdr> globalAdrs;
        private readonly Dictionary<Guid, List<GlobalAdrVersion>> versionsByGlobalId = [];
        private readonly Dictionary<Guid, List<GlobalAdrUpdateProposal>> proposalsByGlobalId = [];
        private readonly Dictionary<(Guid GlobalId, int RepositoryId, int LocalAdrNumber), GlobalAdrInstance> instances = [];

        public FakeGlobalAdrStore(IEnumerable<GlobalAdr>? seedGlobalAdrs = null)
        {
            globalAdrs = (seedGlobalAdrs ?? [])
                .ToDictionary(item => item.GlobalId);
        }

        public int AddCallCount { get; private set; }
        public int UpsertCallCount { get; private set; }
        public GlobalAdr? LastAddedGlobalAdr { get; private set; }
        public GlobalAdrInstance? LastUpsertedInstance { get; private set; }

        public Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdr>>(globalAdrs.Values.OrderBy(item => item.Title).ToArray());
        }

        public Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = globalAdrs.TryGetValue(globalId, out var globalAdr);
            return Task.FromResult(globalAdr);
        }

        public Task<IReadOnlyList<GlobalAdrVersion>> GetVersionsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!versionsByGlobalId.TryGetValue(globalId, out var rows))
            {
                return Task.FromResult<IReadOnlyList<GlobalAdrVersion>>([]);
            }

            return Task.FromResult<IReadOnlyList<GlobalAdrVersion>>(rows.OrderByDescending(item => item.VersionNumber).ToArray());
        }

        public Task<IReadOnlyList<GlobalAdrUpdateProposal>> GetUpdateProposalsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!proposalsByGlobalId.TryGetValue(globalId, out var rows))
            {
                return Task.FromResult<IReadOnlyList<GlobalAdrUpdateProposal>>([]);
            }

            return Task.FromResult<IReadOnlyList<GlobalAdrUpdateProposal>>(rows.OrderByDescending(item => item.CreatedAtUtc).ToArray());
        }

        public Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var rows = instances.Values
                .Where(item => item.GlobalId == globalId)
                .OrderBy(item => item.RepositoryId)
                .ThenBy(item => item.LocalAdrNumber)
                .ToArray();
            return Task.FromResult<IReadOnlyList<GlobalAdrInstance>>(rows);
        }

        public Task<int> GetDashboardPendingCountAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var pendingProposals = proposalsByGlobalId.Values.SelectMany(item => item).Count(item => item.IsPending);
            var updateAvailableInstances = instances.Values.Count(item => item.UpdateAvailable);
            return Task.FromResult(pendingProposals + updateAvailableInstances);
        }

        public Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(globalAdr);
            ct.ThrowIfCancellationRequested();

            AddCallCount++;
            LastAddedGlobalAdr = globalAdr;
            globalAdrs[globalAdr.GlobalId] = globalAdr;
            return Task.FromResult(globalAdr);
        }

        public Task<GlobalAdr?> UpdateAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(globalAdr);
            ct.ThrowIfCancellationRequested();

            if (!globalAdrs.ContainsKey(globalAdr.GlobalId))
            {
                return Task.FromResult<GlobalAdr?>(null);
            }

            globalAdrs[globalAdr.GlobalId] = globalAdr;
            return Task.FromResult<GlobalAdr?>(globalAdr);
        }

        public Task<GlobalAdrVersion> AddVersionAsync(GlobalAdrVersion version, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(version);
            ct.ThrowIfCancellationRequested();

            if (!versionsByGlobalId.TryGetValue(version.GlobalId, out var rows))
            {
                rows = [];
                versionsByGlobalId[version.GlobalId] = rows;
            }

            if (version.Id <= 0)
            {
                version.Id = rows.Count + 1;
            }

            rows.Add(version);
            return Task.FromResult(version);
        }

        public Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();

            if (!proposalsByGlobalId.TryGetValue(proposal.GlobalId, out var rows))
            {
                rows = [];
                proposalsByGlobalId[proposal.GlobalId] = rows;
            }

            if (proposal.Id <= 0)
            {
                proposal.Id = rows.Count + 1;
            }

            rows.Add(proposal);
            return Task.FromResult(proposal);
        }

        public Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();

            if (!proposalsByGlobalId.TryGetValue(proposal.GlobalId, out var rows))
            {
                return Task.FromResult<GlobalAdrUpdateProposal?>(null);
            }

            var index = rows.FindIndex(item => item.Id == proposal.Id);
            if (index < 0)
            {
                return Task.FromResult<GlobalAdrUpdateProposal?>(null);
            }

            rows[index] = proposal;
            return Task.FromResult<GlobalAdrUpdateProposal?>(proposal);
        }

        public Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ct.ThrowIfCancellationRequested();

            UpsertCallCount++;
            LastUpsertedInstance = instance;

            var key = (instance.GlobalId, instance.RepositoryId, instance.LocalAdrNumber);
            if (instances.TryGetValue(key, out var existing))
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
                instance.Id = instances.Count + 1;
            }

            instances[key] = instance;
            return Task.FromResult(instance);
        }
    }
}
