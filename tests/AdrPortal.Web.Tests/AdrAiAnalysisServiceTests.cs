using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrAiAssistantServiceTests
{
    [Test]
    public async Task EvaluateAndRecommendAsync_ReturnsStructuredRecommendation_ForDraftInput()
    {
        var repository = CreateRepository(id: 301);
        IReadOnlyList<Adr> existingAdrs =
        [
            CreateAdr(1, "Use Aspire", "use-aspire", AdrStatus.Accepted, """
# Use Aspire

## Context and Problem Statement

Need local orchestration.

## Decision Drivers

- Keep startup deterministic

## Considered Options

- Use .NET Aspire
- Use custom scripts
"""),
            CreateAdr(2, "Use SQLite", "use-sqlite", AdrStatus.Accepted, """
# Use SQLite

## Context and Problem Statement

Need local persistence.

## Decision Drivers

- Deterministic local environment
""")
        ];

        var managedStore = new FakeManagedRepositoryStore(repository);
        var adrRepository = new FakeAdrFileRepository(existingAdrs);
        var factory = new FakeMadrRepositoryFactory(adrRepository);
        var aiService = new RecordingAiService();
        var service = new AdrAiAssistantService(managedStore, factory, aiService);
        var input = new AdrEditorInput
        {
            Title = "Use platform defaults",
            Slug = "use-platform-defaults",
            Status = AdrStatus.Proposed,
            Date = new DateOnly(2026, 3, 20),
            DecisionMakers = ["Architecture Board"],
            Consulted = [],
            Informed = [],
            BodyMarkdown = """
## Context and Problem Statement

Need to align hosting and telemetry decisions.

## Decision Drivers

- Keep startup deterministic
- Reduce support overhead

## Considered Options

- Use .NET Aspire
- Keep custom scripts
"""
        };

        var result = await service.EvaluateAndRecommendAsync(repository.Id, currentAdrNumber: null, input, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PreferredOption).IsEqualTo("Use .NET Aspire");
        await Assert.That(result.IsFallback).IsTrue();
        await Assert.That(result.Options.Count).IsEqualTo(2);
        await Assert.That(result.Options.All(option => option.Pros.Count > 0)).IsTrue();
        await Assert.That(result.Options.All(option => option.Cons.Count > 0)).IsTrue();
        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(1);
        await Assert.That(factory.CreateCallCount).IsEqualTo(1);
        await Assert.That(aiService.LastEvaluationDraft).IsNotNull();
        await Assert.That(aiService.LastEvaluationDraft!.DecisionDrivers.Count).IsEqualTo(2);
        await Assert.That(aiService.LastEvaluationDraft.ConsideredOptions.Count).IsEqualTo(2);
        await Assert.That(aiService.LastEvaluationExistingAdrs.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EvaluateAndRecommendAsync_ExcludesCurrentAdr_InEditMode()
    {
        var repository = CreateRepository(id: 302);
        IReadOnlyList<Adr> existingAdrs =
        [
            CreateAdr(1, "Use Aspire", "use-aspire", AdrStatus.Accepted, "# Use Aspire"),
            CreateAdr(2, "Use SQLite", "use-sqlite", AdrStatus.Accepted, "# Use SQLite")
        ];

        var managedStore = new FakeManagedRepositoryStore(repository);
        var adrRepository = new FakeAdrFileRepository(existingAdrs);
        var factory = new FakeMadrRepositoryFactory(adrRepository);
        var aiService = new RecordingAiService();
        var service = new AdrAiAssistantService(managedStore, factory, aiService);
        var input = new AdrEditorInput
        {
            Title = "Use SQLite",
            Slug = "use-sqlite",
            Status = AdrStatus.Accepted,
            Date = new DateOnly(2026, 3, 20),
            DecisionMakers = ["Board"],
            Consulted = [],
            Informed = [],
            BodyMarkdown = """
## Context and Problem Statement

Persist data locally.

## Considered Options

- SQLite
"""
        };

        var result = await service.EvaluateAndRecommendAsync(repository.Id, currentAdrNumber: 2, input, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(aiService.LastEvaluationExistingAdrs.Count).IsEqualTo(1);
        await Assert.That(aiService.LastEvaluationExistingAdrs[0].Number).IsEqualTo(1);
    }

    [Test]
    public async Task EvaluateAndRecommendAsync_ReturnsNull_WhenRepositoryMissing()
    {
        var managedStore = new FakeManagedRepositoryStore(repository: null);
        var adrRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(adrRepository);
        var aiService = new RecordingAiService();
        var service = new AdrAiAssistantService(managedStore, factory, aiService);
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

        var result = await service.EvaluateAndRecommendAsync(999, currentAdrNumber: null, input, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(aiService.EvaluateCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task FindAffectedAdrsAsync_ReturnsAnalysis_ForExistingAdr()
    {
        var repository = CreateRepository(id: 303);
        var source = CreateAdr(5, "Adopt Aspire", "adopt-aspire", AdrStatus.Proposed, """
# Adopt Aspire

## Context and Problem Statement

Need deterministic local orchestration and telemetry.

## Decision Drivers

- deterministic orchestration
- telemetry visibility

## Considered Options

- Use .NET Aspire
- Keep scripts
""");
        var related = CreateAdr(2, "Use Aspire", "use-aspire", AdrStatus.Accepted, """
# Use Aspire

## Context and Problem Statement

Need deterministic orchestration.
""");
        var unrelated = CreateAdr(3, "Use FTP", "use-ftp", AdrStatus.Rejected, "# Use FTP for transfer");

        var managedStore = new FakeManagedRepositoryStore(repository);
        var adrRepository = new FakeAdrFileRepository([source, related, unrelated]);
        var factory = new FakeMadrRepositoryFactory(adrRepository);
        var aiService = new RecordingAiService();
        var service = new AdrAiAssistantService(managedStore, factory, aiService);

        var result = await service.FindAffectedAdrsAsync(repository.Id, source.Number, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].AdrNumber).IsEqualTo(2);
        await Assert.That(result.IsFallback).IsTrue();
        await Assert.That(aiService.AffectedCallCount).IsEqualTo(1);
        await Assert.That(aiService.LastAffectedExistingAdrs.Count).IsEqualTo(2);
        await Assert.That(aiService.LastAffectedExistingAdrs.Any(adr => adr.Number == source.Number)).IsFalse();
    }

    [Test]
    public async Task FindAffectedAdrsAsync_ReturnsNull_WhenAdrMissing()
    {
        var repository = CreateRepository(id: 304);
        var managedStore = new FakeManagedRepositoryStore(repository);
        var adrRepository = new FakeAdrFileRepository([]);
        var factory = new FakeMadrRepositoryFactory(adrRepository);
        var aiService = new RecordingAiService();
        var service = new AdrAiAssistantService(managedStore, factory, aiService);

        var result = await service.FindAffectedAdrsAsync(repository.Id, adrNumber: 77, CancellationToken.None);

        await Assert.That(result).IsNull();
        await Assert.That(aiService.AffectedCallCount).IsEqualTo(0);
    }

    private static ManagedRepository CreateRepository(int id)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = $"repo-{id}",
            RootPath = $@"C:\repos\contoso\repo-{id}",
            AdrFolder = "docs/adr",
            InboxFolder = "docs/inbox",
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
        public int CreateCallCount { get; private set; }

        public Task<IAdrFileRepository> CreateAsync(ManagedRepository managedRepository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(managedRepository);
            CreateCallCount++;
            return Task.FromResult<IAdrFileRepository>(repository);
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

    private sealed class RecordingAiService : IAiService
    {
        public int EvaluateCallCount { get; private set; }
        public int AffectedCallCount { get; private set; }
        public AdrDraftForAnalysis? LastEvaluationDraft { get; private set; }
        public IReadOnlyList<Adr> LastEvaluationExistingAdrs { get; private set; } = [];
        public AdrDraftForAnalysis? LastAffectedDraft { get; private set; }
        public IReadOnlyList<Adr> LastAffectedExistingAdrs { get; private set; } = [];

        public Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AdrBootstrapProposalSet
                {
                    RepositoryRootPath = repoRootPath,
                    GeneratedAtUtc = DateTime.UtcNow,
                    ScannedFileCount = 0,
                    Chunks = [],
                    Proposals = []
                });
        }

        public Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            EvaluateCallCount++;
            LastEvaluationDraft = draftAdr;
            LastEvaluationExistingAdrs = existingAdrs;

            var preferred = draftAdr.ConsideredOptions.FirstOrDefault() ?? "No preferred option";
            return Task.FromResult(
                new AdrEvaluationRecommendation
                {
                    PreferredOption = preferred,
                    RecommendationSummary = "Deterministic recommendation generated.",
                    DecisionFit = $"Grounded against {existingAdrs.Count} ADR(s).",
                    Options = draftAdr.ConsideredOptions
                        .Select((option, index) => new AdrOptionRecommendation
                        {
                            OptionName = option,
                            Summary = $"Summary for {option}",
                            Score = Math.Round(0.9 - (index * 0.1), 2, MidpointRounding.AwayFromZero),
                            Rationale = $"Rationale for {option}",
                            Pros = ["Pro"],
                            Cons = ["Con"],
                            TradeOffs = ["Trade-off"]
                        })
                        .ToArray(),
                    Risks = ["Risk 1"],
                    SuggestedAlternatives = ["Alternative A"],
                    GroundingAdrNumbers = existingAdrs.Select(adr => adr.Number).ToArray(),
                    IsFallback = true,
                    FallbackReason = "Configured deterministic AI provider."
                });
        }

        public Task<AffectedAdrAnalysisResult> FindAffectedAdrsAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            AffectedCallCount++;
            LastAffectedDraft = draftAdr;
            LastAffectedExistingAdrs = existingAdrs;

            var items = existingAdrs
                .Where(adr => adr.Title.Contains("Aspire", StringComparison.OrdinalIgnoreCase))
                .Select(adr => new AffectedAdrResultItem
                {
                    AdrNumber = adr.Number,
                    Title = adr.Title,
                    ImpactLevel = AdrImpactLevel.Medium,
                    Rationale = "Shared orchestration signals detected.",
                    Signals = ["aspire", "orchestration"]
                })
                .ToArray();

            return Task.FromResult(
                new AffectedAdrAnalysisResult
                {
                    Items = items,
                    Summary = $"Identified {items.Length} affected ADR(s).",
                    IsFallback = true,
                    FallbackReason = "Configured deterministic AI provider."
                });
        }
    }
}
