using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using AdrPortal.Infrastructure.Ai;

namespace AdrPortal.Infrastructure.Tests;

public class DeterministicAiServiceTests
{
    [Test]
    public async Task BootstrapAdrsFromCodebaseAsync_ReturnsStructuredProposalSet()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "AdrPortal.slnx"), "<Solution />");
            await File.WriteAllTextAsync(Path.Combine(root, "Directory.Packages.props"), "<Project />");
            var srcPath = Path.Combine(root, "src");
            Directory.CreateDirectory(srcPath);
            await File.WriteAllTextAsync(Path.Combine(srcPath, "Program.cs"), """
using Aspire.Hosting;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var distributedBuilder = DistributedApplication.CreateBuilder(args);
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDbContext<AppDbContext>();
""");
            var testsPath = Path.Combine(root, "tests");
            Directory.CreateDirectory(testsPath);
            await File.WriteAllTextAsync(Path.Combine(testsPath, "ProgramTests.cs"), """
using TUnit;

[Test]
public async Task Sample() { }
""");

            var service = new DeterministicAiService();

            var result = await service.BootstrapAdrsFromCodebaseAsync(root, CancellationToken.None);

            await Assert.That(result.RepositoryRootPath).IsEqualTo(Path.GetFullPath(root));
            await Assert.That(result.ScannedFileCount).IsGreaterThan(0);
            await Assert.That(result.Chunks.Count).IsGreaterThan(0);
            await Assert.That(result.Proposals.Count).IsGreaterThan(0);
            await Assert.That(result.Proposals.Any(proposal => proposal.Title.Contains("Aspire", StringComparison.OrdinalIgnoreCase))).IsTrue();
            await Assert.That(result.Proposals.All(proposal => proposal.ConfidenceScore > 0 && proposal.ConfidenceScore <= 1)).IsTrue();
            await Assert.That(result.Proposals.All(proposal => proposal.EvidenceFiles.Count > 0)).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task BootstrapAdrsFromCodebaseAsync_ThrowsWhenRootPathMissing()
    {
        var service = new DeterministicAiService();
        var missingPath = Path.Combine(Path.GetTempPath(), "adr-phase9-missing", Guid.NewGuid().ToString("N"));

        Exception? exception = null;
        try
        {
            _ = await service.BootstrapAdrsFromCodebaseAsync(missingPath, CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is DirectoryNotFoundException).IsTrue();
    }

    [Test]
    public async Task BootstrapAdrsFromCodebaseAsync_ThrowsWhenCancelled()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "AdrPortal.slnx"), "<Solution />");
            var service = new DeterministicAiService();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Exception? exception = null;
            try
            {
                _ = await service.BootstrapAdrsFromCodebaseAsync(root, cts.Token);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            await Assert.That(exception is OperationCanceledException).IsTrue();
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task EvaluateAndRecommendAsync_ReturnsDeterministicFallbackRecommendation()
    {
        var service = new DeterministicAiService();
        var draft = new AdrDraftForAnalysis
        {
            Title = "Adopt Aspire defaults",
            Slug = "adopt-aspire-defaults",
            ProblemStatement = "Need deterministic local orchestration.",
            BodyMarkdown = """
## Context and Problem Statement

Need deterministic local orchestration.

## Decision Drivers

- deterministic startup
- consistency

## Considered Options

- Use .NET Aspire
- Keep custom scripts
""",
            DecisionDrivers = ["deterministic startup", "consistency"],
            ConsideredOptions = ["Use .NET Aspire", "Keep custom scripts"]
        };
        IReadOnlyList<Adr> existingAdrs =
        [
            CreateAdr(1, "Use Aspire", "use-aspire", AdrStatus.Accepted, "# Use Aspire"),
            CreateAdr(2, "Use SQLite", "use-sqlite", AdrStatus.Accepted, "# Use SQLite")
        ];

        var result = await service.EvaluateAndRecommendAsync(draft, existingAdrs, CancellationToken.None);

        await Assert.That(result.IsFallback).IsTrue();
        await Assert.That(result.FallbackReason).IsEqualTo("Configured deterministic AI provider.");
        await Assert.That(result.Options.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(result.PreferredOption).IsEqualTo(result.Options[0].OptionName);
        await Assert.That(result.GroundingAdrNumbers.Count).IsEqualTo(2);
        await Assert.That(result.RecommendationSummary.Contains("Recommend '", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Options.All(option => option.Pros.Count > 0)).IsTrue();
        await Assert.That(result.Options.All(option => option.Cons.Count > 0)).IsTrue();
        await Assert.That(result.Options.All(option => option.Summary.Length > 20)).IsTrue();
        await Assert.That(result.SuggestedAlternatives.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task EvaluateAndRecommendAsync_ThrowsWhenCancelled()
    {
        var service = new DeterministicAiService();
        var draft = new AdrDraftForAnalysis
        {
            Title = "Use queues",
            Slug = "use-queues",
            ProblemStatement = "Need async processing.",
            BodyMarkdown = "## Context and Problem Statement",
            DecisionDrivers = [],
            ConsideredOptions = []
        };
        var existingAdrs = Array.Empty<Adr>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? exception = null;
        try
        {
            _ = await service.EvaluateAndRecommendAsync(draft, existingAdrs, cts.Token);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is OperationCanceledException).IsTrue();
    }

    [Test]
    public async Task GenerateDraftFromQuestionAsync_ReturnsRecommendationWithOptionsAndActiveConstraintFit()
    {
        var service = new DeterministicAiService();
        IReadOnlyList<Adr> existingAdrs =
        [
            CreateAdr(1, "Use Aspire", "use-aspire", AdrStatus.Accepted, "# Use Aspire"),
            CreateAdr(2, "Try queues", "try-queues", AdrStatus.Proposed, "# Try queues"),
            CreateAdr(3, "Reject FTP", "reject-ftp", AdrStatus.Rejected, "# Reject FTP")
        ];

        var result = await service.GenerateDraftFromQuestionAsync(
            "Should we standardize service-to-service communication on gRPC?",
            existingAdrs,
            CancellationToken.None);

        await Assert.That(result.Question).Contains("standardize service-to-service communication", StringComparison.OrdinalIgnoreCase);
        await Assert.That(result.SuggestedTitle).Contains("Decide how to", StringComparison.Ordinal);
        await Assert.That(result.SuggestedSlug).IsNotEmpty();
        await Assert.That(result.Recommendation.Options.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(result.Recommendation.Options.All(option => option.Pros.Count > 0)).IsTrue();
        await Assert.That(result.Recommendation.Options.All(option => option.Cons.Count > 0)).IsTrue();
        await Assert.That(result.Recommendation.PreferredOption).IsEqualTo(result.Recommendation.Options[0].OptionName);
        await Assert.That(result.Recommendation.DecisionFit).Contains("Grounded against", StringComparison.Ordinal);
        await Assert.That(result.Recommendation.GroundingAdrNumbers.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task GenerateDraftFromQuestionAsync_ThrowsWhenCancelled()
    {
        var service = new DeterministicAiService();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? exception = null;
        try
        {
            _ = await service.GenerateDraftFromQuestionAsync(
                "Should we use gRPC?",
                [],
                cts.Token);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is OperationCanceledException).IsTrue();
    }

    [Test]
    public async Task FindAffectedAdrsAsync_ReturnsRankedAffectedItems()
    {
        var service = new DeterministicAiService();
        var draft = new AdrDraftForAnalysis
        {
            Title = "Adopt Aspire defaults",
            Slug = "adopt-aspire-defaults",
            ProblemStatement = "Need deterministic local orchestration.",
            BodyMarkdown = """
## Context and Problem Statement

Need deterministic local orchestration and telemetry.

## Decision Drivers

- deterministic startup

## Considered Options

- Use .NET Aspire
""",
            DecisionDrivers = ["deterministic startup"],
            ConsideredOptions = ["Use .NET Aspire"]
        };
        IReadOnlyList<Adr> existingAdrs =
        [
            CreateAdr(1, "Use Aspire", "use-aspire", AdrStatus.Accepted, """
# Use Aspire

## Context and Problem Statement

Need deterministic local orchestration.
"""),
            CreateAdr(2, "Use FTP", "use-ftp", AdrStatus.Rejected, "# Use FTP")
        ];

        var result = await service.FindAffectedAdrsAsync(draft, existingAdrs, CancellationToken.None);

        await Assert.That(result.IsFallback).IsTrue();
        await Assert.That(result.Items.Count).IsGreaterThan(0);
        await Assert.That(result.Items[0].AdrNumber).IsEqualTo(1);
        await Assert.That(result.Items[0].Signals.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task FindAffectedAdrsAsync_ThrowsWhenCancelled()
    {
        var service = new DeterministicAiService();
        var draft = new AdrDraftForAnalysis
        {
            Title = "Use queues",
            Slug = "use-queues",
            ProblemStatement = "Need async processing.",
            BodyMarkdown = "## Context and Problem Statement",
            DecisionDrivers = [],
            ConsideredOptions = []
        };
        var existingAdrs = Array.Empty<Adr>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Exception? exception = null;
        try
        {
            _ = await service.FindAffectedAdrsAsync(draft, existingAdrs, cts.Token);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is OperationCanceledException).IsTrue();
    }

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "adr-phase9-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
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
}
