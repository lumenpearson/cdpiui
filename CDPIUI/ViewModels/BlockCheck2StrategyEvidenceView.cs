using System;
using System.Collections.Generic;
using System.Linq;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.ViewModels;

/// <summary>
/// Search, filter and sort of the strategy evidence list on the result page. The filter and sort
/// modes are the indexes of the page's combo box items.
/// </summary>
internal static class BlockCheck2StrategyEvidenceView
{
    public static IEnumerable<BlockCheck2StrategyEvidenceItem> Apply(
        IEnumerable<BlockCheck2StrategyEvidenceItem> source,
        string? searchText,
        int sortMode,
        int filterMode)
    {
        string search = (searchText ?? string.Empty).Trim();
        IEnumerable<BlockCheck2StrategyEvidenceItem> query = source;
        if (search.Length > 0)
        {
            query = query.Where(item => Matches(item, search));
        }

        query = Filter(query, filterMode);
        return Sort(query, sortMode);
    }

    private static bool Matches(BlockCheck2StrategyEvidenceItem item, string search) =>
        item.SiteUrl.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        item.ConnectionDetails.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        item.StrategyArguments.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        item.StrategyName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
        item.StrategyId.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<BlockCheck2StrategyEvidenceItem> Filter(
        IEnumerable<BlockCheck2StrategyEvidenceItem> query,
        int filterMode) => filterMode switch
    {
        1 => query.Where(item => item.Status == BlockCheckEvidenceStatus.Successful),
        2 => query.Where(item => item.Status == BlockCheckEvidenceStatus.Partial),
        3 => query.Where(item => item.Status == BlockCheckEvidenceStatus.Failed),
        _ => query,
    };

    private static IEnumerable<BlockCheck2StrategyEvidenceItem> Sort(
        IEnumerable<BlockCheck2StrategyEvidenceItem> query,
        int sortMode) => sortMode switch
    {
        1 => query.OrderBy(item => item.SuccessRate)
            .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
        2 => query.OrderBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(item => item.SuccessRate),
        3 => query.OrderByDescending(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(item => item.SuccessRate),
        4 => query.OrderBy(item => item.ResponseTimeSortValue)
            .ThenByDescending(item => item.SuccessRate),
        5 => query.OrderByDescending(item => item.ResponseTimeSortValue)
            .ThenByDescending(item => item.SuccessRate),
        6 => query.OrderBy(item => item.StrategyArguments, StringComparer.Ordinal)
            .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
        7 => query.OrderByDescending(item => item.StrategyArguments, StringComparer.Ordinal)
            .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
        _ => query.OrderByDescending(item => item.SuccessRate)
            .ThenByDescending(item => item.SuccessCount)
            .ThenBy(item => item.ResponseTimeSortValue)
            .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
    };
}
