using System.Collections.Generic;
using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class RepositoryComparisonServiceTests
{
    [Test]
    public async Task CompareAsync_RanksSourceAdrsByTargetImpactAndPreservesLibraryFlags()
    {
        var sourceRepository = CreateRepository(101, @"C:\repos\source");
        var targetRepository = CreateRepository(202, @"C:\repos\target");
        var sourceGlobalId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sourceAdrs = new[]
        {
            CreateAdr(
                number: 1,
                slug: "service-boundary",
                title: "Service Boundary",
                status: AdrStatus.Accepted,
                globalId: sourceGlobalId,
                globalVersion: 2),
            CreateAdr(
                number: 2,
                slug: "ui-density",
                title: "UI Density",
                status: AdrStatus.Proposed)
        };
        var targetAdrs = new[]
        {
            CreateAdr(number: 10, slug: "api-gateway", title: "API Gateway", status: AdrStatus.Accepted),
            CreateAdr(number: 11, slug: "ux-patterns", title: "UX Patterns", status: AdrStatus.Accepted)
        };

        var managedStore = new FakeManagedRepositoryStore(sourceRepository, targetRepository);
        var factory = new FakeMadrRepositoryFactory(
            CreateRepositoriesById(
                (sourceRepository.Id, sourceAdrs),
                (targetRepository.Id, targetAdrs)));
        var aiService = new FakeAiService(
            affectedResultsBySlug: new Dictionary<string, AffectedAdrAnalysisResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["service-boundary"] = new AffectedAdrAnalysisResult
                {
                    Summary = "High overlap with existing API gateway ADR.",
                    Items =
                    [
                        new AffectedAdrResultItem
                        {
                            AdrNumber = 10,
                            Title = "API Gateway",
                            ImpactLevel = AdrImpactLevel.High,
                            Rationale = "Shared domain terms.",
                            Signals = ["gateway", "service"]
                        }
                    ],
                    IsFallback = true,
                    FallbackReason = "Deterministic fallback"
                },
                ["ui-density"] = new AffectedAdrAnalysisResult
                {
                    Summary = "Limited overlap against target ADR corpus.",
                    Items =
                    [
                        new AffectedAdrResultItem
                        {
                            AdrNumber = 11,
                            Title = "UX Patterns",
                            ImpactLevel = AdrImpactLevel.Low,
                            Rationale = "Only one style keyword shared.",
                            Signals = ["layout"]
                        }
                    ],
                    IsFallback = true,
                    FallbackReason = "Deterministic fallback"
                }
            });
        var globalStore = new FakeGlobalAdrStore();
        var service = new RepositoryComparisonService(managedStore, factory, aiService, globalStore);

        var result = await service.CompareAsync(sourceRepository.Id, targetRepository.Id, CancellationToken.None);

        await Assert.That(result.SourceRepository.Id).IsEqualTo(sourceRepository.Id);
        await Assert.That(result.TargetRepository.Id).IsEqualTo(targetRepository.Id);
        await Assert.That(result.SourceAdrCount).IsEqualTo(2);
        await Assert.That(result.TargetAdrCount).IsEqualTo(2);
        await Assert.That(result.RankedSourceAdrs.Count).IsEqualTo(2);
        await Assert.That(result.RankedSourceAdrs[0].SourceAdrNumber).IsEqualTo(1);
        await Assert.That(result.RankedSourceAdrs[1].SourceAdrNumber).IsEqualTo(2);
        await Assert.That(result.RankedSourceAdrs[0].RelevanceScore).IsGreaterThan(result.RankedSourceAdrs[1].RelevanceScore);
        await Assert.That(result.RankedSourceAdrs[0].IsLibraryLinked).IsTrue();
        await Assert.That(result.RankedSourceAdrs[0].GlobalId).IsEqualTo(sourceGlobalId);
        await Assert.That(result.RankedSourceAdrs[0].GlobalVersion).IsEqualTo(2);
        await Assert.That(result.RankedSourceAdrs[0].TargetMatches.Count).IsEqualTo(1);
        await Assert.That(result.RankedSourceAdrs[0].TargetMatches[0].TargetAdrNumber).IsEqualTo(10);
        await Assert.That(result.RankedSourceAdrs[0].TargetMatches[0].ImpactLevel).IsEqualTo(AdrImpactLevel.High);
        await Assert.That(aiService.FindAffectedCalls.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ImportSelectedAsync_ImportsAsProposedAndPreservesGlobalMapping()
    {
        var sourceRepository = CreateRepository(111, @"C:\repos\source-import");
        var targetRepository = CreateRepository(222, @"C:\repos\target-import");
        var mappedGlobalId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sourceAdrs = new[]
        {
            CreateAdr(
                number: 3,
                slug: "service-layer",
                title: "Service Layer",
                status: AdrStatus.Accepted,
                globalId: mappedGlobalId,
                globalVersion: 4)
        };
        var targetAdrs = new[]
        {
            CreateAdr(number: 1, slug: "service-layer", title: "Existing target ADR", status: AdrStatus.Accepted)
        };

        var managedStore = new FakeManagedRepositoryStore(sourceRepository, targetRepository);
        var factory = new FakeMadrRepositoryFactory(
            CreateRepositoriesById(
                (sourceRepository.Id, sourceAdrs),
                (targetRepository.Id, targetAdrs)));
        var aiService = new FakeAiService(new Dictionary<string, AffectedAdrAnalysisResult>(StringComparer.OrdinalIgnoreCase));
        var globalStore = new FakeGlobalAdrStore(
            globalAdrs:
            [
                new GlobalAdr
                {
                    GlobalId = mappedGlobalId,
                    Title = "Service Layer",
                    CurrentVersion = 5,
                    RegisteredAtUtc = DateTime.UtcNow.AddDays(-10),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]);
        var service = new RepositoryComparisonService(managedStore, factory, aiService, globalStore);

        var result = await service.ImportSelectedAsync(
            sourceRepository.Id,
            targetRepository.Id,
            [3],
            CancellationToken.None);

        await Assert.That(result.ImportedItems.Count).IsEqualTo(1);
        await Assert.That(result.GlobalRegistrationsCreated).IsEqualTo(0);
        await Assert.That(result.GlobalInstancesUpserted).IsEqualTo(1);
        await Assert.That(result.ImportedItems[0].SourceAdrNumber).IsEqualTo(3);
        await Assert.That(result.ImportedItems[0].ImportedTargetAdrNumber).IsEqualTo(2);
        await Assert.That(result.ImportedItems[0].ImportedSlug).IsEqualTo("service-layer-2");
        await Assert.That(result.ImportedItems[0].ImportedStatus).IsEqualTo(AdrStatus.Proposed);
        await Assert.That(result.ImportedItems[0].GlobalId).IsEqualTo(mappedGlobalId);
        await Assert.That(result.ImportedItems[0].GlobalVersion).IsEqualTo(4);

        var targetRepositoryStore = factory.GetRepository(targetRepository.Id);
        var persistedTargetAdrs = await targetRepositoryStore.GetAllAsync(CancellationToken.None);
        var importedAdr = persistedTargetAdrs.Single(adr => adr.Number == 2);
        await Assert.That(importedAdr.Status).IsEqualTo(AdrStatus.Proposed);
        await Assert.That(importedAdr.GlobalId).IsEqualTo(mappedGlobalId);
        await Assert.That(importedAdr.GlobalVersion).IsEqualTo(4);

        var instances = await globalStore.GetInstancesAsync(mappedGlobalId, CancellationToken.None);
        await Assert.That(instances.Count).IsEqualTo(1);
        await Assert.That(instances[0].RepositoryId).IsEqualTo(targetRepository.Id);
        await Assert.That(instances[0].LocalAdrNumber).IsEqualTo(2);
        await Assert.That(instances[0].BaseTemplateVersion).IsEqualTo(4);
        await Assert.That(instances[0].UpdateAvailable).IsTrue();
    }

    [Test]
    public async Task ImportSelectedAsync_CreatesMissingGlobalRegistrationAndInstanceMapping()
    {
        var sourceRepository = CreateRepository(333, @"C:\repos\source-missing-global");
        var targetRepository = CreateRepository(444, @"C:\repos\target-missing-global");
        var missingGlobalId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var sourceAdrs = new[]
        {
            CreateAdr(
                number: 8,
                slug: "event-bus",
                title: "Event Bus",
                status: AdrStatus.Accepted,
                globalId: missingGlobalId,
                globalVersion: 2)
        };
        var managedStore = new FakeManagedRepositoryStore(sourceRepository, targetRepository);
        var factory = new FakeMadrRepositoryFactory(
            CreateRepositoriesById(
                (sourceRepository.Id, sourceAdrs),
                (targetRepository.Id, [])));
        var aiService = new FakeAiService(new Dictionary<string, AffectedAdrAnalysisResult>(StringComparer.OrdinalIgnoreCase));
        var globalStore = new FakeGlobalAdrStore();
        var service = new RepositoryComparisonService(managedStore, factory, aiService, globalStore);

        var result = await service.ImportSelectedAsync(
            sourceRepository.Id,
            targetRepository.Id,
            [8],
            CancellationToken.None);

        await Assert.That(result.GlobalRegistrationsCreated).IsEqualTo(1);
        await Assert.That(result.GlobalInstancesUpserted).IsEqualTo(1);
        await Assert.That(result.ImportedItems[0].GlobalId).IsEqualTo(missingGlobalId);
        await Assert.That(result.ImportedItems[0].GlobalVersion).IsEqualTo(2);

        var global = await globalStore.GetByIdAsync(missingGlobalId, CancellationToken.None);
        await Assert.That(global).IsNotNull();
        await Assert.That(global!.CurrentVersion).IsEqualTo(2);

        var versions = await globalStore.GetVersionsAsync(missingGlobalId, CancellationToken.None);
        await Assert.That(versions.Count).IsEqualTo(1);
        await Assert.That(versions[0].VersionNumber).IsEqualTo(2);

        var instances = await globalStore.GetInstancesAsync(missingGlobalId, CancellationToken.None);
        await Assert.That(instances.Count).IsEqualTo(1);
        await Assert.That(instances[0].RepositoryId).IsEqualTo(targetRepository.Id);
    }

    [Test]
    public async Task ImportSelectedAsync_ThrowsWhenSelectionContainsUnknownSourceAdr()
    {
        var sourceRepository = CreateRepository(555, @"C:\repos\source-unknown");
        var targetRepository = CreateRepository(666, @"C:\repos\target-unknown");
        var managedStore = new FakeManagedRepositoryStore(sourceRepository, targetRepository);
        var factory = new FakeMadrRepositoryFactory(
            CreateRepositoriesById(
                (sourceRepository.Id, [CreateAdr(number: 1, slug: "known", title: "Known ADR", status: AdrStatus.Accepted)]),
                (targetRepository.Id, [])));
        var aiService = new FakeAiService(new Dictionary<string, AffectedAdrAnalysisResult>(StringComparer.OrdinalIgnoreCase));
        var globalStore = new FakeGlobalAdrStore();
        var service = new RepositoryComparisonService(managedStore, factory, aiService, globalStore);

        Exception? exception = null;
        try
        {
            _ = await service.ImportSelectedAsync(sourceRepository.Id, targetRepository.Id, [99], CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception!.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    private static ManagedRepository CreateRepository(int id, string rootPath)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = $"repo-{id}",
            RootPath = rootPath,
            AdrFolder = "docs/adr",
            IsActive = true
        };
    }

    private static Adr CreateAdr(
        int number,
        string slug,
        string title,
        AdrStatus status,
        Guid? globalId = null,
        int? globalVersion = null)
    {
        return new Adr
        {
            Number = number,
            Slug = slug,
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            Title = title,
            Status = status,
            Date = new DateOnly(2026, 3, 20),
            GlobalId = globalId,
            GlobalVersion = globalVersion,
            DecisionMakers = ["Architecture Board"],
            Consulted = [],
            Informed = [],
            RawMarkdown = $"# {title}\n\n## Context and Problem Statement\n\nDetails."
        };
    }

    private static IReadOnlyDictionary<int, InMemoryAdrFileRepository> CreateRepositoriesById(
        params (int RepositoryId, IReadOnlyList<Adr> Adrs)[] values)
    {
        var map = new Dictionary<int, InMemoryAdrFileRepository>();
        foreach (var value in values)
        {
            map[value.RepositoryId] = new InMemoryAdrFileRepository(value.Adrs);
        }

        return map;
    }

    private sealed class FakeManagedRepositoryStore(params ManagedRepository[] repositories) : IManagedRepositoryStore
    {
        private readonly Dictionary<int, ManagedRepository> repositoriesById = repositories.ToDictionary(repository => repository.Id);

        public Task<ManagedRepository?> GetByIdAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            repositoriesById.TryGetValue(repositoryId, out var repository);
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
            var map = repositoriesById
                .Where(pair => repositoryIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            return Task.FromResult<IReadOnlyDictionary<int, ManagedRepository>>(map);
        }

        public Task<ManagedRepository> AddAsync(ManagedRepository repository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            repositoriesById[repository.Id] = repository;
            return Task.FromResult(repository);
        }

        public Task<ManagedRepository?> UpdateAsync(ManagedRepository repository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            repositoriesById[repository.Id] = repository;
            return Task.FromResult<ManagedRepository?>(repository);
        }

        public Task<bool> DeleteAsync(int repositoryId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(repositoriesById.Remove(repositoryId));
        }
    }

    private sealed class FakeMadrRepositoryFactory(IReadOnlyDictionary<int, InMemoryAdrFileRepository> repositoriesById) : IMadrRepositoryFactory
    {
        public Task<IAdrFileRepository> CreateAsync(ManagedRepository repository, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!repositoriesById.TryGetValue(repository.Id, out var adrRepository))
            {
                throw new InvalidOperationException($"No ADR repository configured for repository '{repository.Id}'.");
            }

            return Task.FromResult<IAdrFileRepository>(adrRepository);
        }

        public InMemoryAdrFileRepository GetRepository(int repositoryId)
        {
            if (!repositoriesById.TryGetValue(repositoryId, out var repository))
            {
                throw new InvalidOperationException($"No ADR repository configured for repository '{repositoryId}'.");
            }

            return repository;
        }
    }

    private sealed class InMemoryAdrFileRepository(IReadOnlyList<Adr> seed) : IAdrFileRepository
    {
        private readonly List<Adr> adrs = seed.Select(adr => adr with { }).ToList();

        public Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Adr>>(adrs.OrderBy(adr => adr.Number).Select(adr => adr with { }).ToArray());
        }

        public Task<Adr?> GetByNumberAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var adr = adrs.SingleOrDefault(item => item.Number == number);
            return Task.FromResult(adr is null ? null : adr with { });
        }

        public Task<Adr> WriteAsync(Adr adr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(adr);
            ct.ThrowIfCancellationRequested();
            adrs.RemoveAll(existing => existing.Number == adr.Number);
            adrs.Add(adr with { });
            return Task.FromResult(adr with { });
        }

        public Task MoveToRejectedAsync(int number, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var existing = adrs.Single(item => item.Number == number);
            adrs.Remove(existing);
            adrs.Add(existing with { Status = AdrStatus.Rejected });
            return Task.CompletedTask;
        }

        public Task<int> GetNextNumberAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var next = adrs.Count is 0 ? 1 : adrs.Max(adr => adr.Number) + 1;
            return Task.FromResult(next);
        }
    }

    private sealed class FakeAiService(IReadOnlyDictionary<string, AffectedAdrAnalysisResult> affectedResultsBySlug) : IAiService
    {
        public List<(AdrDraftForAnalysis Draft, int ExistingCount)> FindAffectedCalls { get; } = [];

        public Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
            AdrDraftForAnalysis draftAdr,
            IReadOnlyList<Adr> existingAdrs,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                new AdrEvaluationRecommendation
                {
                    PreferredOption = "N/A",
                    RecommendationSummary = "N/A",
                    DecisionFit = "N/A",
                    Options = [],
                    Risks = [],
                    SuggestedAlternatives = [],
                    GroundingAdrNumbers = [],
                    IsFallback = true,
                    FallbackReason = "Not required for comparison tests."
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
            FindAffectedCalls.Add((draftAdr, existingAdrs.Count));

            if (affectedResultsBySlug.TryGetValue(draftAdr.Slug, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(
                new AffectedAdrAnalysisResult
                {
                    Summary = "No deterministic match configured.",
                    Items = [],
                    IsFallback = true,
                    FallbackReason = "Default fake response."
                });
        }

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
    }

    private sealed class FakeGlobalAdrStore(IReadOnlyCollection<GlobalAdr>? globalAdrs = null) : IGlobalAdrStore
    {
        private readonly Dictionary<Guid, GlobalAdr> globalAdrsById = (globalAdrs ?? []).ToDictionary(item => item.GlobalId);
        private readonly List<GlobalAdrVersion> versions = [];
        private readonly List<GlobalAdrUpdateProposal> proposals = [];
        private readonly List<GlobalAdrInstance> instances = [];
        private int nextVersionId = 1;
        private int nextProposalId = 1;
        private int nextInstanceId = 1;

        public Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            globalAdrsById.TryGetValue(globalId, out var value);
            return Task.FromResult(value is null ? null : CopyGlobal(value));
        }

        public Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GlobalAdr>>(globalAdrsById.Values.Select(CopyGlobal).ToArray());
        }

        public Task<IReadOnlyList<GlobalAdrVersion>> GetVersionsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var values = versions.Where(version => version.GlobalId == globalId).Select(CopyVersion).ToArray();
            return Task.FromResult<IReadOnlyList<GlobalAdrVersion>>(values);
        }

        public Task<IReadOnlyList<GlobalAdrUpdateProposal>> GetUpdateProposalsAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var values = proposals.Where(proposal => proposal.GlobalId == globalId).Select(CopyProposal).ToArray();
            return Task.FromResult<IReadOnlyList<GlobalAdrUpdateProposal>>(values);
        }

        public Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var values = instances.Where(instance => instance.GlobalId == globalId).Select(CopyInstance).ToArray();
            return Task.FromResult<IReadOnlyList<GlobalAdrInstance>>(values);
        }

        public Task<int> GetDashboardPendingCountAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var count = proposals.Count(proposal => proposal.IsPending) + instances.Count(instance => instance.UpdateAvailable);
            return Task.FromResult(count);
        }

        public Task<GlobalAdr> AddAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(globalAdr);
            ct.ThrowIfCancellationRequested();
            var stored = CopyGlobal(globalAdr);
            globalAdrsById[stored.GlobalId] = stored;
            return Task.FromResult(CopyGlobal(stored));
        }

        public Task<GlobalAdr?> UpdateAsync(GlobalAdr globalAdr, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(globalAdr);
            ct.ThrowIfCancellationRequested();
            if (!globalAdrsById.ContainsKey(globalAdr.GlobalId))
            {
                return Task.FromResult<GlobalAdr?>(null);
            }

            var stored = CopyGlobal(globalAdr);
            globalAdrsById[stored.GlobalId] = stored;
            return Task.FromResult<GlobalAdr?>(CopyGlobal(stored));
        }

        public Task<GlobalAdrVersion> AddVersionAsync(GlobalAdrVersion version, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(version);
            ct.ThrowIfCancellationRequested();
            var stored = CopyVersion(version);
            stored.Id = nextVersionId++;
            versions.Add(stored);
            return Task.FromResult(CopyVersion(stored));
        }

        public Task<GlobalAdrUpdateProposal> AddUpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();
            var stored = CopyProposal(proposal);
            stored.Id = nextProposalId++;
            proposals.Add(stored);
            return Task.FromResult(CopyProposal(stored));
        }

        public Task<GlobalAdrUpdateProposal?> UpdateProposalAsync(GlobalAdrUpdateProposal proposal, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ct.ThrowIfCancellationRequested();
            var index = proposals.FindIndex(item => item.Id == proposal.Id);
            if (index < 0)
            {
                return Task.FromResult<GlobalAdrUpdateProposal?>(null);
            }

            proposals[index] = CopyProposal(proposal);
            return Task.FromResult<GlobalAdrUpdateProposal?>(CopyProposal(proposals[index]));
        }

        public Task<GlobalAdrInstance> UpsertInstanceAsync(GlobalAdrInstance instance, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ct.ThrowIfCancellationRequested();

            var index = instances.FindIndex(
                item => item.GlobalId == instance.GlobalId
                    && item.RepositoryId == instance.RepositoryId
                    && item.LocalAdrNumber == instance.LocalAdrNumber);
            if (index >= 0)
            {
                var replacement = CopyInstance(instance);
                replacement.Id = instances[index].Id;
                instances[index] = replacement;
                return Task.FromResult(CopyInstance(replacement));
            }

            var stored = CopyInstance(instance);
            stored.Id = nextInstanceId++;
            instances.Add(stored);
            return Task.FromResult(CopyInstance(stored));
        }

        private static GlobalAdr CopyGlobal(GlobalAdr value)
        {
            return new GlobalAdr
            {
                GlobalId = value.GlobalId,
                Title = value.Title,
                CurrentVersion = value.CurrentVersion,
                RegisteredAtUtc = value.RegisteredAtUtc,
                LastUpdatedAtUtc = value.LastUpdatedAtUtc,
                Instances = [],
                Versions = [],
                UpdateProposals = []
            };
        }

        private static GlobalAdrVersion CopyVersion(GlobalAdrVersion value)
        {
            return new GlobalAdrVersion
            {
                Id = value.Id,
                GlobalId = value.GlobalId,
                VersionNumber = value.VersionNumber,
                Title = value.Title,
                MarkdownContent = value.MarkdownContent,
                CreatedAtUtc = value.CreatedAtUtc
            };
        }

        private static GlobalAdrUpdateProposal CopyProposal(GlobalAdrUpdateProposal value)
        {
            return new GlobalAdrUpdateProposal
            {
                Id = value.Id,
                GlobalId = value.GlobalId,
                RepositoryId = value.RepositoryId,
                LocalAdrNumber = value.LocalAdrNumber,
                ProposedFromVersion = value.ProposedFromVersion,
                ProposedTitle = value.ProposedTitle,
                ProposedMarkdownContent = value.ProposedMarkdownContent,
                IsPending = value.IsPending,
                CreatedAtUtc = value.CreatedAtUtc
            };
        }

        private static GlobalAdrInstance CopyInstance(GlobalAdrInstance value)
        {
            return new GlobalAdrInstance
            {
                Id = value.Id,
                GlobalId = value.GlobalId,
                RepositoryId = value.RepositoryId,
                LocalAdrNumber = value.LocalAdrNumber,
                RepoRelativePath = value.RepoRelativePath,
                LastKnownStatus = value.LastKnownStatus,
                BaseTemplateVersion = value.BaseTemplateVersion,
                HasLocalChanges = value.HasLocalChanges,
                UpdateAvailable = value.UpdateAvailable,
                LastReviewedAtUtc = value.LastReviewedAtUtc
            };
        }
    }
}
