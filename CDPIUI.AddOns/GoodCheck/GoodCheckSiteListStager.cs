using CDPIUI.Core.Basic;

namespace CDPIUI.AddOns.GoodCheck;

/// <summary>
/// Puts the site lists of a run into the CheckLists folder of GoodCheck, merged into one list or
/// copied one by one, and creates one operation for each list GoodCheck checks.
/// </summary>
internal static class GoodCheckSiteListStager
{
    public static void AddMergedSiteList(
        string siteListFolder,
        List<GoodCheckSiteListModel> siteListsToCheck,
        List<GoodCheckSiteListModel> siteLists,
        List<GoodCheckOperationModel> operations)
    {
        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
        int secondsSinceEpoch = (int)t.TotalSeconds;

        string siteList = Path.Combine(siteListFolder, $"{secondsSinceEpoch}_sitelist.txt");

        GoodCheckSiteListMerger.MergeUniquePreserveOrder(siteListsToCheck, siteList);

        GoodCheckSiteListModel model = new()
        {
            SiteListPath = Path.GetFileName(siteList),
            StrategyListPath = Path.GetFileName(siteListsToCheck[0].StrategyListPath),
        };
        siteLists.Add(model);
        operations.Add(CreateOperation(0, Path.GetFileName(siteList), siteList));
    }

    /// <summary>
    /// Copies each list under a unique name and points its model to the copy.
    /// </summary>
    public static void AddSeparateSiteLists(
        string siteListFolder,
        List<GoodCheckSiteListModel> siteListsToCheck,
        List<GoodCheckOperationModel> operations)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < siteListsToCheck.Count; i++)
        {
            var model = siteListsToCheck[i];
            // The same exception that File.Copy threw for a list without a path.
            string path = model.SiteListPath
                ?? throw new ArgumentNullException(nameof(siteListsToCheck), "A site list has no path.");
            // Lists with the same file name from different packs overwrote each other.
            string name = Path.GetFileName(path);
            if (!usedNames.Add(name))
            {
                name = $"{i}_{name}";
                usedNames.Add(name);
            }
            File.Copy(path, Path.Combine(siteListFolder, name), true);

            model.SiteListPath = name;
            model.StrategyListPath = Path.GetFileName(model.StrategyListPath);

            operations.Add(CreateOperation(i, model.SiteListPath, path));
        }
    }

    // The merged lists of earlier runs ({epoch}_sitelist.txt) were never removed.
    public static void RemoveOldMergedSiteLists(string siteListFolder)
    {
        try
        {
            if (!Directory.Exists(siteListFolder))
                return;
            foreach (string file in Directory.EnumerateFiles(siteListFolder, "*_sitelist.txt"))
            {
                string prefix = Path.GetFileName(file).Split('_')[0];
                if (prefix.All(char.IsDigit) && File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-1))
                    File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessService), $"Cannot remove old site lists: {ex.Message}");
        }
    }

    private static GoodCheckOperationModel CreateOperation(int operationId, string siteListName, string siteListPath)
    {
        CancellationTokenSource cancellationTokenSource = new();
        return new()
        {
            OperationId = operationId,
            SiteListName = siteListName,
            SiteListPath = siteListPath,
            CancellationTokenSource = cancellationTokenSource,
            CancellationToken = cancellationTokenSource.Token
        };
    }
}
