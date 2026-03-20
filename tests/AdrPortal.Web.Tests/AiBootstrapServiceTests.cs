using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Core.Workflows;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AiBootstrapServiceTests
{
    [Test]
    public async Task GenerateProposalSetAsync_ThrowsWhenRepositoryAlreadyHasAdrs()
    {
        var repository = CreateRepository(100, @"C:\repos\contoso\app");
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([
            CreateAdr(number: 1, title: "Existing ADR", slug: "existing-adr", status: AdrStatus.Accepted)
        ]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var aiService = new FakeAiService(CreateProposalSet(repository.RootPath, []));
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        Exception? exception = null;
        try
        {
            _ = await service.GenerateProposalSetAsync(repository.Id, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("no ADRs", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(aiService.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GenerateProposalSetAsync_DelegatesToAiServiceWhenRepositoryIsEmpty()
    {
        var repository = CreateRepository(101, @"C:\repos\contoso\empty");
        var proposalSet = CreateProposalSet(repository.RootPath, [
            CreateProposal("p1", "Use Aspire", "use-aspire")
        ]);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var aiService = new FakeAiService(proposalSet);
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        var result = await service.GenerateProposalSetAsync(repository.Id, CancellationToken.None);

        await Assert.That(result.Proposals.Count).IsEqualTo(1);
        await Assert.That(result.Proposals[0].ProposalId).IsEqualTo("p1");
        await Assert.That(aiService.CallCount).IsEqualTo(1);
        await Assert.That(aiService.LastRepoRootPath).IsEqualTo(repository.RootPath);
    }

    [Test]
    public async Task AcceptSelectedProposalsAsync_WritesProposedAdrsAndQueuesWorkflowItems()
    {
        var repository = CreateRepository(102, @"C:\repos\contoso\new-repo");
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var proposalSet = CreateProposalSet(repository.RootPath, [
            CreateProposal(
                proposalId: "p1",
                title: "Use .NET Aspire for local orchestration",
                slug: "use-dotnet-aspire-local-orchestration",
                confidence: 0.92),
            CreateProposal(
                proposalId: "p2",
                title: "Persist portal configuration with EF Core and SQLite",
                slug: "persist-portal-configuration-efcore-sqlite",
                confidence: 0.88)
        ]);
        var aiService = new FakeAiService(proposalSet);
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        var result = await service.AcceptSelectedProposalsAsync(repository.Id, proposalSet, ["p1", "p2"], CancellationToken.None);

        await Assert.That(result.CreatedAdrs.Count).IsEqualTo(2);
        await Assert.That(result.CreatedAdrs.All(adr => adr.Status == AdrStatus.Proposed)).IsTrue();
        await Assert.That(result.CreatedAdrs[0].Number).IsEqualTo(1);
        await Assert.That(result.CreatedAdrs[1].Number).IsEqualTo(2);
        await Assert.That(result.CreatedAdrs[0].DecisionMakers.Count).IsEqualTo(1);
        await Assert.That(result.CreatedAdrs[0].DecisionMakers[0]).IsEqualTo("ADR Portal AI Bootstrap");
        await Assert.That(result.CreatedAdrs[0].RawMarkdown.Contains("## Context and Problem Statement", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.QueuedItems.Count).IsEqualTo(2);
        await Assert.That(result.QueuedItems[0].Trigger).IsEqualTo(GitPrWorkflowTrigger.AiBootstrap);
        await Assert.That(result.QueuedItems[0].Action).IsEqualTo(GitPrWorkflowAction.CreateOrUpdatePullRequest);
        await Assert.That(result.QueuedItems[0].RepoRelativePath).IsEqualTo(result.CreatedAdrs[0].RepoRelativePath);
        await Assert.That(result.QueuedItems[0].BaseBranchName).IsEqualTo("master");
        await Assert.That(result.QueuedItems[0].RepositoryRemoteUrl).IsEqualTo(repository.GitRemoteUrl);
        await Assert.That(result.QueuedItems[0].BranchName).IsEqualTo("proposed/adr-0001-use-dotnet-aspire-local-orchestration");
        await Assert.That(result.QueuedItems[1].BranchName).IsEqualTo("proposed/adr-0002-persist-portal-configuration-efcore-sqlite");
        await Assert.That(queue.Items.Count).IsEqualTo(2);
        await Assert.That(fileRepository.WrittenAdrs.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AcceptSelectedProposalsAsync_ThrowsWhenGitRemoteUrlMissing()
    {
        var repository = CreateRepository(106, @"C:\repos\contoso\missing-remote");
        repository.GitRemoteUrl = null;
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var proposalSet = CreateProposalSet(repository.RootPath, [
            CreateProposal("p1", "Use Aspire", "use-aspire")
        ]);
        var aiService = new FakeAiService(proposalSet);
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        Exception? exception = null;
        try
        {
            _ = await service.AcceptSelectedProposalsAsync(repository.Id, proposalSet, ["p1"], CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("Git remote URL", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(queue.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AcceptSelectedProposalsAsync_ThrowsWhenSelectionContainsUnknownProposal()
    {
        var repository = CreateRepository(103, @"C:\repos\contoso\selection");
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var proposalSet = CreateProposalSet(repository.RootPath, [
            CreateProposal("p1", "Use Aspire", "use-aspire")
        ]);
        var aiService = new FakeAiService(proposalSet);
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        Exception? exception = null;
        try
        {
            _ = await service.AcceptSelectedProposalsAsync(repository.Id, proposalSet, ["missing"], CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("could not be resolved", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(fileRepository.WrittenAdrs.Count).IsEqualTo(0);
        await Assert.That(queue.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AcceptSelectedProposalsAsync_ThrowsWhenSelectionIsEmpty()
    {
        var repository = CreateRepository(104, @"C:\repos\contoso\selection-empty");
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var proposalSet = CreateProposalSet(repository.RootPath, [
            CreateProposal("p1", "Use Aspire", "use-aspire")
        ]);
        var aiService = new FakeAiService(proposalSet);
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        Exception? exception = null;
        try
        {
            _ = await service.AcceptSelectedProposalsAsync(repository.Id, proposalSet, [], CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("at least one", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task AcceptSelectedProposalsAsync_NormalizesDuplicateSlugs()
    {
        var repository = CreateRepository(105, @"C:\repos\contoso\slug-collision");
        var managedStore = new FakeManagedRepositoryStore(repository);
        var fileRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(fileRepository);
        var proposalSet = CreateProposalSet(repository.RootPath, [
            CreateProposal("p1", "Use Aspire", "use-aspire"),
            CreateProposal("p2", "Use Aspire 2", "use-aspire")
        ]);
        var aiService = new FakeAiService(proposalSet);
        var queue = new RecordingQueue();
        var service = new AiBootstrapService(managedStore, factory, aiService, queue);

        var result = await service.AcceptSelectedProposalsAsync(repository.Id, proposalSet, ["p1", "p2"], CancellationToken.None);

        await Assert.That(result.CreatedAdrs.Count).IsEqualTo(2);
        await Assert.That(result.CreatedAdrs[0].Slug).IsEqualTo("use-aspire");
        await Assert.That(result.CreatedAdrs[1].Slug).IsEqualTo("use-aspire-2");
    }

    private static ManagedRepository CreateRepository(int id, string rootPath)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = $"repo-{id}",
            RootPath = rootPath,
            AdrFolder = "docs/adr",
            GitRemoteUrl = $"https://github.com/contoso/repo-{id}.git",
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
            Date = new DateOnly(2026, 3, 20),
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            DecisionMakers = ["Team"],
            Consulted = [],
            Informed = [],
            RawMarkdown = $"# {title}"
        };
    }

    private static AdrBootstrapProposalSet CreateProposalSet(string rootPath, IReadOnlyList<AdrDraftProposal> proposals)
    {
        return new AdrBootstrapProposalSet
        {
            RepositoryRootPath = rootPath,
            GeneratedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            ScannedFileCount = 5,
            Chunks =
            [
                new CodebaseScanChunk
                {
                    ChunkId = "chunk-001",
                    RepoRelativePath = "src/Program.cs",
                    Preview = "builder.AddServiceDefaults();",
                    CharacterCount = 28
                }
            ],
            Proposals = proposals
        };
    }

    private static AdrDraftProposal CreateProposal(string proposalId, string title, string slug, double confidence = 0.8)
    {
        return new AdrDraftProposal
        {
            ProposalId = proposalId,
            Title = title,
            SuggestedSlug = slug,
            ProblemStatement = "Need an architectural decision.",
            DecisionDrivers = ["Keep implementation consistent"],
            InitialOptions = ["Option A", "Option B"],
            EvidenceChunkIds = ["chunk-001"],
            EvidenceFiles = ["src/Program.cs"],
            ConfidenceScore = confidence
        };
    }

    private sealed class FakeManagedRepositoryStore(ManagedRepository repository) : IManagedRepositoryStore
    {
        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repository.Id == repositoryId ? repository : null);
        }

        public Task<IReadOnlyList<ManagedRepository>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ManagedRepository>>([repository]);
        }

        public Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repositoryIds);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(
                repositoryIds.Contains(repository.Id)
                    ? new Dictionary<int, ManagedRepository> { [repository.Id] = repository }
                    : new Dictionary<int, ManagedRepository>());
        }

        public Task<ManagedRepository> AddAsync(ManagedRepository managedRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(managedRepository);
        }

        public Task<ManagedRepository?> UpdateAsync(ManagedRepository managedRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<ManagedRepository?>(managedRepository);
        }

        public Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repositoryId == repository.Id);
        }
    }

    private sealed class FakeMadrRepositoryFactory(FakeAdrFileRepository repository) : IMadrRepositoryFactory
    {
        public Task<IAdrFileRepository> CreateAsync(ManagedRepository managedRepository, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(managedRepository);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IAdrFileRepository>(repository);
        }
    }

    private sealed class FakeAdrFileRepository(IReadOnlyList<Adr> initialAdrs) : IAdrFileRepository
    {
        private readonly List<Adr> adrs = [.. initialAdrs];
        public List<Adr> WrittenAdrs { get; } = [];

        public Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Adr>>(adrs.OrderBy(adr => adr.Number).ToArray());
        }

        public Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(adrs.SingleOrDefault(adr => adr.Number == number));
        }

        public Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(adr);
            ct.ThrowIfCancellationRequested();
            adrs.RemoveAll(existing => existing.Number == adr.Number);
            adrs.Add(adr);
            WrittenAdrs.Add(adr);
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
            return Task.FromResult(adrs.Count is 0 ? 1 : adrs.Max(adr => adr.Number) + 1);
        }
    }

    private sealed class FakeAiService(AdrBootstrapProposalSet proposalSet) : IAiService
    {
        public int CallCount { get; private set; }
        public string? LastRepoRootPath { get; private set; }

        public Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(draftAdr);
            ArgumentNullException.ThrowIfNull(existingAdrs);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AdrEvaluationRecommendation
                {
                    PreferredOption = "Deterministic option",
                    RecommendationSummary = "Not used by bootstrap tests.",
                    DecisionFit = "N/A",
                    Options = [],
                    Risks = [],
                    SuggestedAlternatives = [],
                    GroundingAdrNumbers = [],
                    IsFallback = true,
                    FallbackReason = "Configured deterministic AI provider."
                });
        }

        public Task<AffectedAdrAnalysisResult> FindAffectedAdrsAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(draftAdr);
            ArgumentNullException.ThrowIfNull(existingAdrs);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AffectedAdrAnalysisResult
                {
                    Items = [],
                    Summary = "No affected ADRs.",
                    IsFallback = true,
                    FallbackReason = "Configured deterministic AI provider."
                });
        }

        public Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            LastRepoRootPath = repoRootPath;
            return Task.FromResult(proposalSet);
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
            var existingIndex = Items.FindIndex(existing => existing.Id == item.Id);
            if (existingIndex >= 0)
            {
                Items[existingIndex] = item;
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
}
