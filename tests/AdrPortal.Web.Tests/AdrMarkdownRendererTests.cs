using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrMarkdownRendererTests
{
    [Test]
    public async Task Render_RemovesYamlFrontMatterFromRenderedHtml()
    {
        var renderer = new AdrMarkdownRenderer();
        var markdown = """
---
status: accepted
date: 2026-03-19
decision-makers: [Architecture Board]
---
# Sample ADR

## Context and Problem Statement

Content paragraph.
""";

        var html = renderer.Render(markdown);

        await Assert.That(html.Contains("decision-makers", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(html.Contains("<h1>Sample ADR</h1>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<h2>Context and Problem Statement</h2>", StringComparison.Ordinal)).IsTrue();
    }
}
