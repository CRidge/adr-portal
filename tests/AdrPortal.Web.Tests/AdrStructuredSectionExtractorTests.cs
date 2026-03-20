using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrStructuredSectionExtractorTests
{
    [Test]
    public async Task Extract_ParsesProsConsAndDecisionOutcome()
    {
        var extractor = new AdrStructuredSectionExtractor();
        var markdown = """
            ---
            status: accepted
            date: 2026-03-19
            ---
            # Test ADR

            ## Decision Outcome

            Chosen option: "Option A", because it offers the best tradeoff.

            ### Consequences

            * Good, because implementation is straightforward.
            * Bad, because migration effort is required.

            ## Pros and Cons of the Options

            ### Option A
            * Good, because balanced.
            * Bad, because moderate complexity.

            ### Option B
            * Good, because familiar stack.
            * Bad, because lower scalability.
            """;

        var structured = extractor.Extract(markdown);

        await Assert.That(structured.SelectedOption).IsEqualTo("Option A");
        await Assert.That(structured.Rationale).Contains("best tradeoff");
        await Assert.That(structured.ConsideredOptions.Count).IsEqualTo(2);
        await Assert.That(structured.ConsideredOptions[0].Name).IsEqualTo("Option A");
        await Assert.That(structured.ConsideredOptions[0].Pros.Count).IsEqualTo(1);
        await Assert.That(structured.ConsideredOptions[0].Cons.Count).IsEqualTo(1);
        await Assert.That(structured.PositiveConsequences.Count).IsEqualTo(1);
        await Assert.That(structured.NegativeConsequences.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Extract_FallsBackToSimpleConsideredOptionsWhenProsConsSectionMissing()
    {
        var extractor = new AdrStructuredSectionExtractor();
        var markdown = """
            # Test ADR

            ## Considered Options

            * Option 1
            * Option 2
            * Option 3
            """;

        var structured = extractor.Extract(markdown);

        await Assert.That(structured.ConsideredOptions.Count).IsEqualTo(3);
        await Assert.That(structured.ConsideredOptions[0].Name).IsEqualTo("Option 1");
        await Assert.That(structured.ConsideredOptions[0].Pros.Count).IsEqualTo(0);
        await Assert.That(structured.ConsideredOptions[0].Cons.Count).IsEqualTo(0);
    }
}
