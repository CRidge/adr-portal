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
}
