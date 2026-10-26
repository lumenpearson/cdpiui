using System.Runtime.ExceptionServices;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.Store.Network.Models;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Models;

namespace CDPIUI.Core.Store;

/// <summary>
/// Asks the version control of every installed item for its latest release and lists the
/// items that have a newer version.
/// </summary>
internal sealed class StoreUpdateChecker
{
    // Update checks sent at the same time; more would hit the API rate limits sooner.
    private const int UpdateCheckConcurrency = 4;

    private static readonly string LogSource = $"{nameof(StoreHelper)}/{nameof(StoreHelper.CheckUpdates)}";

    private readonly APIWorker _apiWorker;
    private readonly Func<string?, string?, string?> _resolveRepoUrl;

    /// <param name="resolveRepoUrl">
    /// Gives the URL to check from the stored URL and the store id of an item, or null when
    /// the item has nothing to check.
    /// </param>
    public StoreUpdateChecker(APIWorker apiWorker, Func<string?, string?, string?> resolveRepoUrl)
    {
        _apiWorker = apiWorker;
        _resolveRepoUrl = resolveRepoUrl;
    }

    /// <summary>
    /// Adds the available updates to <paramref name="updates"/> in the order of the installed
    /// items. Returns whether a check failed. An error while preparing the list is thrown
    /// after the items before it were checked.
    /// </summary>
    public async Task<bool> FindUpdatesAsync(List<ItemUpdateAvailableModel> updates)
    {
        List<(DatabaseStoreItem Item, string RepoUrl)> itemsToCheck =
            CollectItemsToCheck(out ExceptionDispatchInfo? prepareError);

        var versionRequests = await RequestLatestVersions(itemsToCheck);

        bool exceptionHappens = false;
        for (int i = 0; i < itemsToCheck.Count; i++)
        {
            var (item, repoUrl) = itemsToCheck[i];
            var versionDataCheckResult = await versionRequests[i];

            if (versionDataCheckResult.ErrorHappens || !versionDataCheckResult.Success)
            {
                Logger.Instance.CreateWarningLog(
                    LogSource,
                    $"Cannot check updates for {item.Id}, with version control type {item.VersionControlType}. Uri used to check {repoUrl} " +
                    $"Exception information: {versionDataCheckResult.Error.ErrorCode}");
                exceptionHappens = true;
                continue;
            }

            if (!TryAddUpdate(updates, item, versionDataCheckResult.Result))
                exceptionHappens = true;
        }

        prepareError?.Throw();
        return exceptionHappens;
    }

    private List<(DatabaseStoreItem Item, string RepoUrl)> CollectItemsToCheck(out ExceptionDispatchInfo? prepareError)
    {
        List<DatabaseStoreItem> storeItems = DatabaseHelper.Instance.GetAllInstalledItems();

        List<(DatabaseStoreItem Item, string RepoUrl)> itemsToCheck = [];
        prepareError = null;
        foreach (var item in storeItems)
        {
            if (item.Id == SharedConstants.LocalUserItemsId || item.VersionControlType == "local")
                continue;

            string? repoUrl;
            try
            {
                repoUrl = _resolveRepoUrl(item.UpdateCheckUrl, item.Id);
            }
            catch (Exception ex)
            {
                // The items before this one are still checked, as when they were checked one by one.
                prepareError = ExceptionDispatchInfo.Capture(ex);
                break;
            }
            // Local packs have no update source; that is not an error.
            if (string.IsNullOrEmpty(repoUrl))
                continue;

            itemsToCheck.Add((item, repoUrl));
        }
        return itemsToCheck;
    }

    // The requests run a few at a time; the results are handled in the item order.
    private async Task<List<Task<OperationResultModel<ReleaseInfoModel>>>> RequestLatestVersions(
        List<(DatabaseStoreItem Item, string RepoUrl)> itemsToCheck)
    {
        using SemaphoreSlim requestSlots = new(UpdateCheckConcurrency);
        var versionRequests = itemsToCheck
            .Select(async x =>
            {
                await requestSlots.WaitAsync().ConfigureAwait(false);
                try
                {
                    return await _apiWorker.GetLastVersionAndVersionNotes(x.RepoUrl).ConfigureAwait(false);
                }
                finally
                {
                    requestSlots.Release();
                }
            })
            .ToList();
        try
        {
            await Task.WhenAll(versionRequests);
        }
        catch
        {
            // Each failed request is handled at its place in the list.
        }
        return versionRequests;
    }

    private static bool TryAddUpdate(
        List<ItemUpdateAvailableModel> updates,
        DatabaseStoreItem item,
        ReleaseInfoModel? versionData)
    {
        try
        {
            string curV = item.CurrentVersion;
            string serV = versionData.ReleaseTag;

            if (VersionHelper.CompareVersionStrings(curV, serV) == -1)
            {
                updates.Add(new()
                {
                    StoreId = item.Id,
                    CurrentVersion = item.CurrentVersion,
                    ServerVersion = versionData.ReleaseTag,
                    VersionInfo = versionData.ReleaseNotes,
                });
            }
            return true;
        }
        catch
        {
            Logger.Instance.CreateWarningLog(
                LogSource,
                $"Cannot compare versions {item.CurrentVersion}&&{versionData.ReleaseNotes} for {item.Id}");
            return false;
        }
    }
}
