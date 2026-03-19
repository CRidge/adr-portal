using AdrPortal.Core.Entities;
using System.Globalization;

namespace AdrPortal.Web.Services;

/// <summary>
/// Projects ADR domain values into route-specific list and detail view models.
/// </summary>
public sealed class AdrListViewService
{
    /// <summary>
    /// Creates projected list items and filter options for the ADR list route.
    /// </summary>
    /// <param name="adrs">ADRs to project.</param>
    /// <param name="searchTerm">Optional search term.</param>
    /// <param name="statusFilter">Optional status filter.</param>
    /// <returns>Projected list and filter metadata.</returns>
    public AdrListProjectionResult BuildList(IReadOnlyList<Adr> adrs, string? searchTerm, AdrStatus? statusFilter)
    {
        ArgumentNullException.ThrowIfNull(adrs);

        var allItems = adrs
            .OrderByDescending(adr => adr.Number)
            .Select(MapToListItem)
            .ToArray();

        var normalizedSearch = NormalizeSearch(searchTerm);
        var filterOptions = BuildFilterOptions(allItems);
        var filteredItems = allItems
            .Where(item => MatchesSearch(item, normalizedSearch))
            .Where(item => statusFilter is null || item.Status == statusFilter.Value)
            .ToArray();

        return new AdrListProjectionResult
        {
            AllItems = allItems,
            FilteredItems = filteredItems,
            FilterOptions = filterOptions
        };
    }

    /// <summary>
    /// Maps an ADR to a detail view model.
    /// </summary>
    /// <param name="adr">ADR domain model.</param>
    /// <param name="htmlContent">Rendered markdown HTML.</param>
    /// <returns>Detail view model.</returns>
    public AdrDetailViewModel BuildDetail(Adr adr, string htmlContent)
    {
        ArgumentNullException.ThrowIfNull(adr);
        ArgumentNullException.ThrowIfNull(htmlContent);

        return new AdrDetailViewModel
        {
            Number = adr.Number,
            Title = adr.Title,
            Slug = adr.Slug,
            Status = adr.Status,
            StatusView = AdrStatusViewModel.FromStatus(adr.Status),
            Date = adr.Date,
            RepoRelativePath = adr.RepoRelativePath,
            GlobalId = adr.GlobalId,
            GlobalVersion = adr.GlobalVersion,
            SupersededByNumber = adr.SupersededByNumber,
            DecisionMakers = adr.DecisionMakers,
            Consulted = adr.Consulted,
            Informed = adr.Informed,
            HtmlContent = htmlContent
        };
    }

    private static string NormalizeSearch(string? searchTerm)
    {
        return searchTerm?.Trim() ?? string.Empty;
    }

    private static AdrListItem MapToListItem(Adr adr)
    {
        return new AdrListItem
        {
            Number = adr.Number,
            Title = adr.Title,
            Status = adr.Status,
            StatusView = AdrStatusViewModel.FromStatus(adr.Status),
            Date = adr.Date,
            Slug = adr.Slug
        };
    }

    private static IReadOnlyList<AdrFilterOption> BuildFilterOptions(IReadOnlyList<AdrListItem> allItems)
    {
        var statuses = Enum.GetValues<AdrStatus>();
        var counts = allItems
            .GroupBy(item => item.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        var options = statuses
            .Select(status =>
            {
                var statusView = AdrStatusViewModel.FromStatus(status);
                return new AdrFilterOption
                {
                    Status = status,
                    Label = statusView.Label,
                    CssModifier = statusView.CssModifier,
                    Count = counts.GetValueOrDefault(status, 0)
                };
            })
            .ToArray();

        return options;
    }

    private static bool MatchesSearch(AdrListItem item, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return true;
        }

        var formattedNumber = item.Number.ToString("0000", CultureInfo.InvariantCulture);
        var adrToken = $"ADR-{formattedNumber}";

        return item.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || item.Slug.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || item.Number.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || formattedNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || adrToken.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || item.StatusView.Label.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }
}
