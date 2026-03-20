using AdrPortal.Core.Entities;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class RepositoryInboxUploadServiceTests
{
    [Test]
    public async Task ImportAsync_ImportsMarkdownAndReportsNonMarkdownFailures()
    {
        var repository = CreateRepository(10);
        var importService = new FakeInboxImportService(repository);
        var uploadService = new RepositoryInboxUploadService(importService);
        var uploads = new[]
        {
            new RepositoryInboxUploadDocument("proposal.md", "# Proposal"),
            new RepositoryInboxUploadDocument("notes.txt", "# Notes")
        };

        var outcome = await uploadService.ImportAsync(repository.Id, uploads, CancellationToken.None);

        await Assert.That(outcome.ImportedAdrs.Count).IsEqualTo(1);
        await Assert.That(outcome.ImportedAdrs[0].Number).IsEqualTo(1);
        await Assert.That(outcome.Failures.Count).IsEqualTo(1);
        await Assert.That(outcome.Failures[0].Contains(".md", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(importService.MarkdownImportCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ImportAsync_SanitizesUploadFileNamesBeforePipelineCall()
    {
        var repository = CreateRepository(11);
        var importService = new FakeInboxImportService(repository);
        var uploadService = new RepositoryInboxUploadService(importService);
        var uploads = new[]
        {
            new RepositoryInboxUploadDocument(@"..\nested\proposal.md", "# Proposal")
        };

        _ = await uploadService.ImportAsync(repository.Id, uploads, CancellationToken.None);

        await Assert.That(importService.LastMarkdownFileName).IsEqualTo("proposal.md");
    }

    [Test]
    public async Task ImportAsync_ReturnsFailureWhenRepositoryIsMissing()
    {
        var repository = CreateRepository(12);
        var importService = new FakeInboxImportService(repository)
        {
            ReturnNullForMarkdownImport = true
        };
        var uploadService = new RepositoryInboxUploadService(importService);
        var uploads = new[]
        {
            new RepositoryInboxUploadDocument("proposal.md", "# Proposal")
        };

        var outcome = await uploadService.ImportAsync(repository.Id, uploads, CancellationToken.None);

        await Assert.That(outcome.ImportedAdrs.Count).IsEqualTo(0);
        await Assert.That(outcome.Failures.Count).IsEqualTo(1);
        await Assert.That(outcome.Failures[0].Contains("not found", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(importService.MarkdownImportCallCount).IsEqualTo(1);
    }

    private static ManagedRepository CreateRepository(int id)
    {
        return new ManagedRepository
        {
            Id = id,
            DisplayName = $"repo-{id}",
            RootPath = $@"C:\repos\repo-{id}",
            AdrFolder = "docs/adr",
            InboxFolder = "docs/inbox",
            GitRemoteUrl = $"https://github.com/contoso/repo-{id}.git",
            IsActive = true
        };
    }

    private sealed class FakeInboxImportService(ManagedRepository repository) : IInboxImportService
    {
        public int MarkdownImportCallCount { get; private set; }

        public string? LastMarkdownFileName { get; private set; }

        public bool ReturnNullForMarkdownImport { get; init; }

        public Task<InboxImportResult?> ImportInboxFileAsync(int repositoryId, string inboxFilePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<InboxImportResult?>(null);
        }

        public Task<InboxImportResult?> ImportInboxMarkdownAsync(int repositoryId, string sourceFileName, string markdown, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            MarkdownImportCallCount++;
            LastMarkdownFileName = sourceFileName;

            if (ReturnNullForMarkdownImport)
            {
                return Task.FromResult<InboxImportResult?>(null);
            }

            var importedAdr = new Adr
            {
                Number = MarkdownImportCallCount,
                Slug = "proposal",
                RepoRelativePath = $"docs/adr/adr-{MarkdownImportCallCount:0000}-proposal.md",
                Title = "Proposal",
                Status = AdrStatus.Proposed,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                RawMarkdown = markdown
            };

            return Task.FromResult<InboxImportResult?>(new InboxImportResult
            {
                Repository = repository,
                ImportedAdr = importedAdr,
                Message = "ok"
            });
        }
    }
}
