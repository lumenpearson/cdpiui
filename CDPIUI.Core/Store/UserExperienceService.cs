using System.Linq;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared.Extensions;

namespace CDPIUI.Core;

internal interface IUserExperienceService
{
    /// <summary>
    /// Ger requirements for ItemId
    /// </summary>
    /// <param name="storeId">Target Item Id</param>
    /// <returns>List of requirements if exist, otherwise <see cref="null"/> </returns>
    List<Tuple<string, string>>? GetItemRequiredItemsById(string storeId);


    /// <summary>
    /// Get similar items for ItemId
    /// </summary>
    /// <param name="storeId">Target Item Id, to search similar with</param>
    /// <returns>List of similar items if available, otherwise empty list</returns>
    List<RepoItemModel> GetSimilarItemsForStoreId(string storeId);
}

internal class UserExperienceService : IUserExperienceService
{
    private readonly StoreHelper _store;
    public UserExperienceService(StoreHelper store) { _store = store; }

    public List<Tuple<string, string>>? GetItemRequiredItemsById(string storeId)
    {
        if (!DatabaseHelper.Instance.IsItemInstalled(storeId))
            return null;

        List<Tuple<string, string>> requiredItems = [];

        DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(storeId);
        requiredItems = item.RequiredItemIds;

        return requiredItems;
    }

    public List<RepoItemModel> GetSimilarItemsForStoreId(string storeId)
    {
        List<RepoItemModel> items = [];
        RepoItemModel item = _store.GetItemInfoFromStoreId(storeId);

        foreach (RepoItemModel itemToCheck in _store.ItemsList)
        {
            if (itemToCheck.store_id is null || itemToCheck.dependencies is null)
                continue;

            if (itemToCheck.store_id == storeId)
                continue;

            if (itemToCheck.type != "configlist")
                continue;
            items.AddRange(
                itemToCheck.dependencies
                .Where(depensety => depensety.Length > 0 && depensety[0] == storeId && !DatabaseHelper.Instance.IsItemInstalled(itemToCheck.store_id))
                .Select(depensety => itemToCheck));
        }

        if (!DatabaseHelper.Instance.IsItemInstalled(HardcodedItemIds.AddOnsIds.FirstOrDefault(x => x.Key == AddOns.GoodCheck).Value) &&
            // GetKeyByValue gives default(Components), which is Zapret, for an id that is no
            // component, so GoodCheck was suggested on the page of every item.
            HardcodedItemIds.ComponentIds.ContainsValue(storeId) &&
            HardcodedItemIds.GoodCheckSupportedComponents.Contains(HardcodedItemIds.ComponentIds.GetKeyByValue(storeId)))
        {
            RepoItemModel? goodCheck = _store.GetItemInfoFromStoreId(HardcodedItemIds.AddOnsIds.FirstOrDefault(x => x.Key == AddOns.GoodCheck).Value);
            if (goodCheck != null)
                items.Add(goodCheck);
        }

        return items;
    }
}
