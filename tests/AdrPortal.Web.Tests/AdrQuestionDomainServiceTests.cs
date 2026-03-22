using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrQuestionDomainServiceTests
{
    [Test]
    public async Task GenerateForRepositoryAsync_UsesAcceptedAdrsAsConstraintsAndReturnsProposedDraft()
    {
        var repository = CreateRepository(id: 601);
        IReadOnlyList<Adr> repositoryAdrs =
        [
            CreateAdr(
                number: 1,
                title: "Use TUnit for .NET tests",
                slug: "use-tunit-for-dotnet-tests",
                status: AdrStatus.Accepted,
                rawMarkdown: "# Use TUnit"),
            CreateAdr(
                number: 2,
                title: "Use SQLite for local data",
                slug: "use-sqlite-local-data",
                status: AdrStatus.Accepted,
                rawMarkdown: "# Use SQLite"),
            CreateAdr(
                number: 3,
                title: "Spike alternate test tooling",
                slug: "spike-alternate-test-tooling",
                status: AdrStatus.Proposed,
                rawMarkdown: "# Spike")
        ];

        var managedStore = new FakeManagedRepositoryStore(repository);
        var adrRepository = new FakeAdrFileRepository(repositoryAdrs);
        var repositoryFactory = new FakeMadrRepositoryFactory(adrRepository);
        var globalStore = new FakeGlobalAdrStore();
        var aiService = new RecordingAiService();
        var service = new AdrQuestionDomainService(managedStore, repositoryFactory, globalStore, aiService);

        var result = await service.GenerateForRepositoryAsync(
            repository.Id,
            "What unit test framework should we use for dotnet unit tests?",
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ContextMode).IsEqualTo(AdrQuestionContextMode.Repository);
        await Assert.That(result.DraftStatus).IsEqualTo(AdrStatus.Proposed);
        await Assert.That(result.ConsideredOptions.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(result.ConsideredOptions.All(option => option.Pros.Count > 0)).IsTrue();
        await Assert.That(result.ConsideredOptions.All(option => option.Cons.Count > 0)).IsTrue();
        await Assert.That(result.DraftMarkdownBody.Contains("## Context and Problem Statement", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.DraftMarkdownBody.Contains("## Considered Options", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.DraftMarkdownBody.Contains("## Decision Outcome", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.DraftMarkdownBody.Contains("Final option selection and ADR acceptance must be performed manually.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Recommendation.IsFallback).IsTrue();

        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(1);
        await Assert.That(aiService.LastExistingAdrs.Count).IsEqualTo(2);
        await Assert.That(aiService.LastExistingAdrs.All(adr => adr.Status == AdrStatus.Accepted)).IsTrue();
        await Assert.That(aiService.LastDraft).IsNotNull();
        await Assert.That(aiService.LastDraft!.DecisionDrivers.Any(driver => driver.Contains("active constraints", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task GenerateForGlobalLibraryAsync_UsesGuidanceOnlyContext()
    {
        var repository = CreateRepository(id: 602);
        var globalId = Guid.Parse("ba8f39a3-fec4-4ca0-a91d-63461f5d10f4");
        var managedStore = new FakeManagedRepositoryStore(repository);
        var repositoryFactory = new FakeMadrRepositoryFactory(new FakeAdrFileRepository([]));
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = globalId,
                    Title = "Testing strategy baseline",
                    CurrentVersion = 2,
                    RegisteredAtUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    LastUpdatedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
                }
            ],
            versions:
            [
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 1,
                    Title = "Testing strategy baseline",
                    MarkdownContent = "# v1",
                    CreatedAtUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
                },
                new GlobalAdrVersion
                {
                    GlobalId = globalId,
                    VersionNumber = 2,
                    Title = "Testing strategy with TUnit",
                    MarkdownContent = "# v2",
                    CreatedAtUtc = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        var aiService = new RecordingAiService();
        var service = new AdrQuestionDomainService(managedStore, repositoryFactory, globalStore, aiService);

        var result = await service.GenerateForGlobalLibraryAsync(
            globalId,
            "How should global testing guidance evolve for dotnet teams?",
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ContextMode).IsEqualTo(AdrQuestionContextMode.GlobalLibrary);
        await Assert.That(result.DraftStatus).IsEqualTo(AdrStatus.Proposed);
        await Assert.That(result.DraftMarkdownBody.Contains("guidance only", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(result.DraftMarkdownBody.Contains("Final option selection and ADR acceptance must be performed manually.", StringComparison.Ordinal)).IsTrue();

        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(1);
        await Assert.That(aiService.LastExistingAdrs.Count).IsEqualTo(2);
        await Assert.That(aiService.LastExistingAdrs.All(adr => adr.Status == AdrStatus.Proposed)).IsTrue();
        await Assert.That(aiService.LastDraft).IsNotNull();
        await Assert.That(aiService.LastDraft!.DecisionDrivers.Any(driver => driver.Contains("guidance", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(aiService.LastDraft.DecisionDrivers.Any(driver => driver.Contains("not mandatory constraints", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task GenerateForRepositoryAsync_ReturnsNull_WhenRepositoryMissing()
    {
        var managedStore = new FakeManagedRepositoryStore(repository: null);
        var repositoryFactory = new FakeMadrRepositoryFactory(new FakeAdrFileRepository([]));
        var globalStore = new FakeGlobalAdrStore();
        var aiService = new RecordingAiService();
        var service = new AdrQuestionDomainService(managedStore, repositoryFactory, globalStore, aiService);

        var result = await service.GenerateForRepositoryAsync(
            repositoryId: 999,
            question: "What unit test framework should we use for dotnet unit tests?",
            CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GenerateForGlobalLibraryAsync_ReturnsNull_WhenGlobalAdrMissing()
    {
        var managedStore = new FakeManagedRepositoryStore(CreateRepository(id: 603));
        var repositoryFactory = new FakeMadrRepositoryFactory(new FakeAdrFileRepository([]));
        var globalStore = new FakeGlobalAdrStore();
        var aiService = new RecordingAiService();
        var service = new AdrQuestionDomainService(managedStore, repositoryFactory, globalStore, aiService);

        var result = await service.GenerateForGlobalLibraryAsync(
            Guid.Parse("95da95b3-2674-4f11-bfa3-cf909be5a594"),
            "How should global testing guidance evolve for dotnet teams?",
            CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GenerateForRepositoryAsync_ThrowsArgumentException_WhenQuestionTooShort()
    {
        var managedStore = new FakeManagedRepositoryStore(CreateRepository(id: 604));
        var repositoryFactory = new FakeMadrRepositoryFactory(new FakeAdrFileRepository([]));
        var globalStore = new FakeGlobalAdrStore();
        var aiService = new RecordingAiService();
        var service = new AdrQuestionDomainService(managedStore, repositoryFactory, globalStore, aiService);

        Exception? exception = null;
        try
        {
            _ = await service.GenerateForRepositoryAsync(604, "short", CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(0);
    }

    private static ManagedRepository CreateRepository(int id)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = $"repo-{id}",
            RootPath = $@"C:\repos\contoso\repo-{id}",
            AdrFolder = "docs/adr",
            GitRemoteUrl = $"https://github.com/contoso/repo-{id}.git",
            IsActive = true
        };
    }

    private static Adr CreateAdr(int number, string title, string slug, AdrStatus status, string rawMarkdown)
    {
        return new Adr
        {
            Number = number,
            Title = title,
            Slug = slug,
            Status = status,
            Date = new DateOnly(2026, 3, 20),
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            DecisionMakers = ["Board"],
            Consulted = [],
            Informed = [],
            RawMarkdown = rawMarkdown
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
            return Task.FromResult<IReadOnlyList<ManagedRepository>>(repository is null ? [] : [repository]);
        }

        public Task<IReadOnlyDictionary<int, ManagedRepository>> GetByIdsAsync(IReadOnlyCollection<int> repositoryIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(repositoryIds);
            ct.ThrowIfCancellationRequested();

            if (repository is null || !repositoryIds.Contains(repository.Id))
            {
                return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(new Dictionary<int, ManagedRepository>());
            }

            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(
                new Dictionary<int, ManagedRepository> { [repository.Id] = repository });
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
            return Task.FromResult(repository?.Id == repositoryId);
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

    private sealed class FakeAdrFileRepository(IReadOnlyList<Adr> initialAdrs) : IAdrFileRepository
    {
        private readonly List<Adr> adrs = [.. initialAdrs];

        public Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Adr>>(adrs.OrderBy(item => item.Number).ToArray());
        }

        public Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(adrs.SingleOrDefault(item => item.Number == number));
        }

        public Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(adr);
            ct.ThrowIfCancellationRequested();
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
            return Task.FromResult(adrs.Count is 0 ? 1 : adrs.Max(item => item.Number) + 1);
        }
    }

    private sealed class FakeGlobalAdrStore : IGlobalAdrStore
    {
        private readonly Dictionary<Guid, GlobalAdr> globalAdrsById;
        private readonly List<GlobalAdrVersion> versions;

        public FakeGlobalAdrStore(
            IReadOnlyList<GlobalAdr>? globalAdrs = null,
            IReadOnlyList<GlobalAdrVersion>? versions = null)
        {
            globalAdrsById = (globalAdrs ?? []).ToDictionary(item => item.GlobalId);
            this.versions = [.. versions ?? []];
        }

        public Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _ = globalAdrsById.TryGetValue(globalId, out var globalAdr);
            return Task.FromResult(globalAdr);
        }

        public Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdr>>(globalAdrsById.Values.OrderBy(item => item.GlobalId).ToArray());
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
            return Task.FromResult<IReadOnlyList<GlobalAdrUpdateProposal>>([]);
        }

        public Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdrInstance>>([]);
        }

        public Task<int> GetDashboardPendingCountAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(0);
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
            versions.Add(version);
            return Task.FromResult(version);
        }

        public Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(proposal);
        }

        public Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<GlobalAdrUpdateProposal?>(proposal);
        }

        public Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(instance);
        }
    }

    private sealed class RecordingAiService : IAiService
    {
        public int EvaluateCallCount { get; private set; }
        public AdrDraftForAnalysis? LastDraft { get; private set; }
        public IReadOnlyList<Adr> LastExistingAdrs { get; private set; } = [];

        public Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            EvaluateCallCount++;
            LastDraft = draftAdr;
            LastExistingAdrs = existingAdrs;

            return Task.FromResult(
                new AdrEvaluationRecommendation
                {
                    PreferredOption = "Adopt TUnit as the standard .NET unit test framework",
                    RecommendationSummary = "TUnit aligns with existing repository patterns while keeping deterministic test execution and simpler tooling consistency.",
                    DecisionFit = $"Grounded against {existingAdrs.Count} ADR(s).",
                    Options =
                    [
                        new AdrOptionRecommendation
                        {
                            OptionName = "Adopt TUnit as the standard .NET unit test framework",
                            Summary = "Use TUnit as the default for all new .NET test projects.",
                            Score = 0.91,
                            Rationale = "Strong consistency with current guidance.",
                            Pros = ["Aligns with current repository defaults.", "Reduces mixed-framework maintenance overhead."],
                            Cons = ["Requires migration planning for non-TUnit suites."],
                            TradeOffs = ["Consistency over incremental flexibility."]
                        },
                        new AdrOptionRecommendation
                        {
                            OptionName = "Allow mixed frameworks by team",
                            Summary = "Teams choose their preferred framework per project.",
                            Score = 0.57,
                            Rationale = "Flexible but creates uneven operational patterns.",
                            Pros = ["Maximum team autonomy."],
                            Cons = ["Increased maintenance and training complexity."],
                            TradeOffs = ["Flexibility over consistency."]
                        }
                    ],
                    Risks = ["Partial migration could delay consistency gains."],
                    SuggestedAlternatives = ["Pilot TUnit on selected repositories first."],
                    GroundingAdrNumbers = existingAdrs.Select(adr => adr.Number).ToArray(),
                    IsFallback = true,
                    FallbackReason = "Test stub deterministic output."
                });
        }

        public Task<AffectedAdrAnalysisResult> FindAffectedAdrsAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AffectedAdrAnalysisResult
                {
                    Summary = "No-op for question service tests.",
                    Items = [],
                    IsFallback = true,
                    FallbackReason = "Not used in this test."
                });
        }

        public Task<AdrQuestionGenerationResult> GenerateDraftFromQuestionAsync(
            string question,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AdrQuestionGenerationResult
                {
                    Question = question,
                    SuggestedTitle = "Unused in this test path",
                    SuggestedSlug = "unused-in-this-test-path",
                    ProblemStatement = "Unused in this test path",
                    DecisionDrivers = [],
                    Recommendation = new AdrEvaluationRecommendation
                    {
                        PreferredOption = "Unused",
                        RecommendationSummary = "Unused",
                        DecisionFit = "Unused",
                        Options = [],
                        Risks = [],
                        SuggestedAlternatives = [],
                        GroundingAdrNumbers = [],
                        IsFallback = true
                    }
                });
        }

        public Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AdrBootstrapProposalSet
                {
                    RepositoryRootPath = @"C:\repos\adr",
                    GeneratedAtUtc = DateTime.UtcNow,
                    ScannedFileCount = 0,
                    Chunks = [],
                    Proposals = []
                });
        }
    }
}
