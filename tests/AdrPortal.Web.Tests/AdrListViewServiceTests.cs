using AdrPortal.Core.Entities;
using AdrPortal.Web.Services;

namespace AdrPortal.Web.Tests;

public class AdrListViewServiceTests
{
    [Test]
    public async Task BuildList_FiltersByStatusAndSearch()
    {
        var service = new AdrListViewService();
        var adrs = new[]
        {
            CreateAdr(number: 1, title: "Use PostgreSQL", slug: "use-postgresql", status: AdrStatus.Accepted),
            CreateAdr(number: 2, title: "Introduce API Gateway Caching", slug: "introduce-api-gateway-caching", status: AdrStatus.Proposed),
            CreateAdr(number: 3, title: "Retire Legacy ETL", slug: "retire-legacy-etl", status: AdrStatus.Superseded)
        };

        var result = service.BuildList(adrs, "gateway", AdrStatus.Proposed);

        await Assert.That(result.AllItems.Count).IsEqualTo(3);
        await Assert.That(result.FilteredItems.Count).IsEqualTo(1);
        await Assert.That(result.FilteredItems[0].Number).IsEqualTo(2);
        await Assert.That(result.FilterOptions.Count).IsEqualTo(5);
        await Assert.That(result.FilterOptions.Single(option => option.Status == AdrStatus.Proposed).Count).IsEqualTo(1);
        await Assert.That(result.FilterOptions.Single(option => option.Status == AdrStatus.Accepted).Count).IsEqualTo(1);
    }

    [Test]
    public async Task BuildList_SearchByAdrPrefixAndPaddedNumber_FindsExpectedItem()
    {
        var service = new AdrListViewService();
        var adrs = new[]
        {
            CreateAdr(number: 9, title: "Adopt API Composition", slug: "adopt-api-composition", status: AdrStatus.Accepted),
            CreateAdr(number: 12, title: "Deprecate Legacy RPC", slug: "deprecate-legacy-rpc", status: AdrStatus.Deprecated),
            CreateAdr(number: 24, title: "Introduce API Gateway Caching", slug: "introduce-api-gateway-caching", status: AdrStatus.Proposed)
        };

        var byPrefix = service.BuildList(adrs, "ADR-0012", statusFilter: null);
        var byPaddedNumber = service.BuildList(adrs, "0024", statusFilter: null);

        await Assert.That(byPrefix.FilteredItems.Count).IsEqualTo(1);
        await Assert.That(byPrefix.FilteredItems[0].Number).IsEqualTo(12);
        await Assert.That(byPaddedNumber.FilteredItems.Count).IsEqualTo(1);
        await Assert.That(byPaddedNumber.FilteredItems[0].Number).IsEqualTo(24);
    }

    [Test]
    public async Task BuildDetail_MapsStatusBadgeAndMetadata()
    {
        var service = new AdrListViewService();
        var adr = CreateAdr(number: 12, title: "Deprecate Legacy RPC", slug: "deprecate-legacy-rpc", status: AdrStatus.Deprecated);
        var detail = service.BuildDetail(adr with
        {
            GlobalId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GlobalVersion = 5,
            SupersededByNumber = 14
        }, "<h1>Deprecate Legacy RPC</h1>");

        await Assert.That(detail.Number).IsEqualTo(12);
        await Assert.That(detail.StatusView.Label).IsEqualTo("Deprecated");
        await Assert.That(detail.StatusView.CssModifier).IsEqualTo("deprecated");
        await Assert.That(detail.GlobalVersion).IsEqualTo(5);
        await Assert.That(detail.SupersededByNumber).IsEqualTo(14);
        await Assert.That(detail.HtmlContent).IsEqualTo("<h1>Deprecate Legacy RPC</h1>");
    }

    private static Adr CreateAdr(int number, string title, string slug, AdrStatus status)
    {
        return new Adr
        {
            Number = number,
            Title = title,
            Slug = slug,
            Status = status,
            Date = new DateOnly(2026, 3, 19),
            RepoRelativePath = $"docs/adr/adr-{number:0000}-{slug}.md",
            DecisionMakers = ["Architecture Board"],
            Consulted = ["Security Team"],
            Informed = ["Platform Guild"],
            RawMarkdown = $"# {title}"
        };
    }
}
