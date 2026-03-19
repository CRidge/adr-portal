using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;

namespace AdrPortal.Core.Tests;

public class MadrWriterTests
{
    [Test]
    public async Task Write_RoundTripsAdrMetadataAndBody()
    {
        var writer = new MadrWriter();
        var parser = new MadrParser();

        var adr = new Adr
        {
            Number = 4,
            Slug = "use-queues",
            RepoRelativePath = "docs/adr/adr-0004-use-queues.md",
            Title = "Use Queues",
            Status = AdrStatus.Accepted,
            Date = new DateOnly(2026, 3, 19),
            GlobalId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            GlobalVersion = 2,
            DecisionMakers = ["Team A", "Team B"],
            Consulted = ["Security"],
            Informed = ["Developers"],
            SupersededByNumber = 12,
            RawMarkdown = """
---
status: proposed
date: 2026-01-01
decision-makers: []
consulted: []
informed: []
---
# Use Queues

## Context and Problem Statement

Event processing requires buffering.
"""
        };

        var markdown = writer.Write(adr);
        var parsed = parser.Parse(adr.RepoRelativePath, markdown);

        await Assert.That(markdown.Contains("status: accepted", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markdown.Contains("global-id: 22222222-2222-2222-2222-222222222222", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markdown.Contains("Event processing requires buffering.", StringComparison.Ordinal)).IsTrue();

        await Assert.That(parsed.Number).IsEqualTo(4);
        await Assert.That(parsed.Slug).IsEqualTo("use-queues");
        await Assert.That(parsed.Status).IsEqualTo(AdrStatus.Accepted);
        await Assert.That(parsed.Date).IsEqualTo(new DateOnly(2026, 3, 19));
        await Assert.That(parsed.GlobalId).IsEqualTo(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        await Assert.That(parsed.GlobalVersion).IsEqualTo(2);
        await Assert.That(parsed.SupersededByNumber).IsEqualTo(12);
        await Assert.That(parsed.DecisionMakers.Count).IsEqualTo(2);
        await Assert.That(parsed.DecisionMakers[0]).IsEqualTo("Team A");
        await Assert.That(parsed.DecisionMakers[1]).IsEqualTo("Team B");
        await Assert.That(parsed.Consulted.Count).IsEqualTo(1);
        await Assert.That(parsed.Consulted[0]).IsEqualTo("Security");
        await Assert.That(parsed.Informed.Count).IsEqualTo(1);
        await Assert.That(parsed.Informed[0]).IsEqualTo("Developers");
    }

    [Test]
    public async Task Write_BuildsTemplateBodyWhenRawMarkdownIsEmpty()
    {
        var writer = new MadrWriter();

        var adr = new Adr
        {
            Number = 18,
            Slug = "introduce-inbox-watcher",
            RepoRelativePath = "docs/adr/adr-0018-introduce-inbox-watcher.md",
            Title = "Introduce Inbox Watcher",
            Status = AdrStatus.Proposed,
            Date = new DateOnly(2026, 3, 19),
            DecisionMakers = ["Portal Team"],
            Consulted = [],
            Informed = []
        };

        var markdown = writer.Write(adr);

        await Assert.That(markdown.Contains("# Introduce Inbox Watcher", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markdown.Contains("## Context and Problem Statement", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markdown.Contains("status: proposed", StringComparison.Ordinal)).IsTrue();
    }
}
