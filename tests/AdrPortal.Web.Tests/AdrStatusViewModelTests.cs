using AdrPortal.Core.Entities;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrStatusViewModelTests
{
    [Test]
    [Arguments(AdrStatus.Proposed, "Proposed", "proposed")]
    [Arguments(AdrStatus.Accepted, "Accepted", "accepted")]
    [Arguments(AdrStatus.Rejected, "Rejected", "rejected")]
    [Arguments(AdrStatus.Superseded, "Superseded", "superseded")]
    [Arguments(AdrStatus.Deprecated, "Deprecated", "deprecated")]
    public async Task FromStatus_ReturnsExpectedBadgeValues(AdrStatus status, string expectedLabel, string expectedCssModifier)
    {
        var viewModel = AdrStatusViewModel.FromStatus(status);

        await Assert.That(viewModel.Label).IsEqualTo(expectedLabel);
        await Assert.That(viewModel.CssModifier).IsEqualTo(expectedCssModifier);
        await Assert.That(viewModel.ShortLabel).IsEqualTo(expectedLabel.ToUpperInvariant());
    }
}
