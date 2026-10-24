using System.Collections.Generic;
using System.Linq;
using CDPIUI.Core.Store.ViewModels;

namespace CDPIUI.Helper.Items;

internal static class StoreItemLicenses
{
    /// <summary>
    /// Returns the licenses of the items, each license (name and URL) only once.
    /// </summary>
    internal static List<ItemLicenseModel> CollectDistinct(IEnumerable<RepoItemModel> items) =>
        items
            .SelectMany(item => item.license ?? [])
            .GroupBy(license => $"{license.name}\n{license.url}")
            .Select(group => group.First())
            .ToList();
}
