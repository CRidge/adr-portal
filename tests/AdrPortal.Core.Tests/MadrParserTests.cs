using AdrPortal.Core.Entities;
using AdrPortal.Core.Madr;

namespace AdrPortal.Core.Tests;

public class MadrParserTests
{
    [Test]
    public async Task Parse_ReadsMadrFrontMatterAndMarkdownBody()
    {
        var parser = new MadrParser();
        var markdown = """
---
status: accepted
date: 2026-03-19
decision-makers: [Alice, Bob]
consulted:
  - Security Team
informed: [Platform Guild]
global-id: "11111111-1111-1111-1111-111111111111"
global-version: 3
superseded-by: 9
---
# Use PostgreSQL

## Context and Problem Statement

Need transactional consistency.
""";

        var adr = parser.Parse("docs/adr/adr-0007-use-postgresql.md", markdown);

        await Assert.That(adr.Number).IsEqualTo(7);
        await Assert.That(adr.Slug).IsEqualTo("use-postgresql");
        await Assert.That(adr.RepoRelativePath).IsEqualTo("docs/adr/adr-0007-use-postgresql.md");
        await Assert.That(adr.Title).IsEqualTo("Use PostgreSQL");
        await Assert.That(adr.Status).IsEqualTo(AdrStatus.Accepted);
        await Assert.That(adr.Date).IsEqualTo(new DateOnly(2026, 3, 19));
        await Assert.That(adr.GlobalId).IsEqualTo(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        await Assert.That(adr.GlobalVersion).IsEqualTo(3);
        await Assert.That(adr.SupersededByNumber).IsEqualTo(9);
        await Assert.That(adr.DecisionMakers.Count).IsEqualTo(2);
        await Assert.That(adr.DecisionMakers[0]).IsEqualTo("Alice");
        await Assert.That(adr.DecisionMakers[1]).IsEqualTo("Bob");
        await Assert.That(adr.Consulted.Count).IsEqualTo(1);
        await Assert.That(adr.Consulted[0]).IsEqualTo("Security Team");
        await Assert.That(adr.Informed.Count).IsEqualTo(1);
        await Assert.That(adr.Informed[0]).IsEqualTo("Platform Guild");
        await Assert.That(adr.RawMarkdown).IsEqualTo(markdown);
    }

    [Test]
    public async Task Parse_UsesDefaultsWhenFrontMatterIsMissing()
    {
        var parser = new MadrParser();
        var markdown = """
## Context and Problem Statement

A title heading is intentionally missing.
""";

        var adr = parser.Parse("docs\\adr\\adr-0012-enable-caching.md", markdown);

        await Assert.That(adr.Status).IsEqualTo(AdrStatus.Proposed);
        await Assert.That(adr.GlobalId).IsNull();
        await Assert.That(adr.GlobalVersion).IsNull();
        await Assert.That(adr.DecisionMakers.Count).IsEqualTo(0);
        await Assert.That(adr.Consulted.Count).IsEqualTo(0);
        await Assert.That(adr.Informed.Count).IsEqualTo(0);
        await Assert.That(adr.Title).IsEqualTo("Enable Caching");
        await Assert.That(adr.RepoRelativePath).IsEqualTo("docs/adr/adr-0012-enable-caching.md");
    }

    [Test]
    public async Task Parse_ThrowsFormatExceptionForUnsupportedStatus()
    {
        var parser = new MadrParser();
        var markdown = """
---
status: pending-review
date: 2026-03-19
---
# Example
""";

        Exception? exception = null;
        try
        {
            _ = parser.Parse("docs/adr/adr-0001-example.md", markdown);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is FormatException).IsTrue();
    }
}
