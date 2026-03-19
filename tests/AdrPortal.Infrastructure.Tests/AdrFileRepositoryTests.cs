using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;
using AdrPortal.Infrastructure.Repositories;

namespace AdrPortal.Infrastructure.Tests;

public class AdrFileRepositoryTests
{
    [Test]
    public async Task WriteAndRead_PersistsAdrFileAndReturnsParsedAdr()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository(root);
            var sourceAdr = CreateAdr(1, "use-postgresql", AdrStatus.Proposed);

            var written = await repository.WriteAsync(sourceAdr, CancellationToken.None);
            var reloaded = await repository.GetByNumberAsync(1, CancellationToken.None);
            var allAdrs = await repository.GetAllAsync(CancellationToken.None);
            var nextNumber = await repository.GetNextNumberAsync(CancellationToken.None);

            var expectedPath = Path.Combine(root, "docs", "adr", "adr-0001-use-postgresql.md");
            await Assert.That(File.Exists(expectedPath)).IsTrue();
            await Assert.That(written.RepoRelativePath).IsEqualTo("docs/adr/adr-0001-use-postgresql.md");
            await Assert.That(reloaded).IsNotNull();
            await Assert.That(reloaded!.Title).IsEqualTo(sourceAdr.Title);
            await Assert.That(allAdrs.Count).IsEqualTo(1);
            await Assert.That(allAdrs[0].Number).IsEqualTo(1);
            await Assert.That(nextNumber).IsEqualTo(2);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task MoveToRejected_MovesFileAndUpdatesStatus()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository(root);
            var sourceAdr = CreateAdr(2, "reconsider-architecture", AdrStatus.Proposed);

            _ = await repository.WriteAsync(sourceAdr, CancellationToken.None);
            await repository.MoveToRejectedAsync(2, CancellationToken.None);
            var rejectedAdr = await repository.GetByNumberAsync(2, CancellationToken.None);

            var proposedPath = Path.Combine(root, "docs", "adr", "adr-0002-reconsider-architecture.md");
            var rejectedPath = Path.Combine(root, "docs", "adr", "rejected", "adr-0002-reconsider-architecture.md");

            await Assert.That(File.Exists(proposedPath)).IsFalse();
            await Assert.That(File.Exists(rejectedPath)).IsTrue();
            await Assert.That(rejectedAdr).IsNotNull();
            await Assert.That(rejectedAdr!.Status).IsEqualTo(AdrStatus.Rejected);
            await Assert.That(rejectedAdr.RepoRelativePath).IsEqualTo("docs/adr/rejected/adr-0002-reconsider-architecture.md");
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task GetAll_ReturnsOrderedNumbersAcrossMainAndRejectedFolders()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = CreateRepository(root);

            _ = await repository.WriteAsync(CreateAdr(10, "late-decision", AdrStatus.Proposed), CancellationToken.None);
            _ = await repository.WriteAsync(CreateAdr(2, "early-rejection", AdrStatus.Rejected), CancellationToken.None);

            var allAdrs = await repository.GetAllAsync(CancellationToken.None);

            await Assert.That(allAdrs.Count).IsEqualTo(2);
            await Assert.That(allAdrs[0].Number).IsEqualTo(2);
            await Assert.That(allAdrs[0].Status).IsEqualTo(AdrStatus.Rejected);
            await Assert.That(allAdrs[1].Number).IsEqualTo(10);
            await Assert.That(allAdrs[1].Status).IsEqualTo(AdrStatus.Proposed);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Test]
    public async Task Constructor_RejectsTraversalSegmentsInAdrFolderPath()
    {
        Exception? exception = null;
        try
        {
            _ = new AdrFileRepository(
                repositoryRootPath: CreateTemporaryDirectory(),
                adrFolderRelativePath: "docs/../adr",
                madrParser: new MadrParser(),
                madrWriter: new MadrWriter());
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    private static AdrFileRepository CreateRepository(string rootPath)
    {
        return new AdrFileRepository(
            repositoryRootPath: rootPath,
            adrFolderRelativePath: "docs/adr",
            madrParser: new MadrParser(),
            madrWriter: new MadrWriter());
    }

    private static Adr CreateAdr(int number, string slug, AdrStatus status)
    {
        var title = BuildTitle(slug);
        return new Adr
        {
            Number = number,
            Slug = slug,
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            Title = title,
            Status = status,
            Date = new DateOnly(2026, 3, 19),
            DecisionMakers = ["Portal Team"],
            Consulted = [],
            Informed = [],
            RawMarkdown = $"""
---
status: {status.ToString().ToLowerInvariant()}
date: 2026-03-19
decision-makers: [Portal Team]
consulted: []
informed: []
---
# {title}

## Context and Problem Statement

Decision details for {title}.
"""
        };
    }

    private static string BuildTitle(string slug)
    {
        return string.Join(
            ' ',
            slug.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }

    private static string CreateTemporaryDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "adr-phase2-tests", Guid.NewGuid().ToString("N"));
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
