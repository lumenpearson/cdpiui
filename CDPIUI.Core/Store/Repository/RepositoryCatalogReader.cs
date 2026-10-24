using CDPIUI.Core.Basic;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.ViewModels;

namespace CDPIUI.Core.Store.Repository;

/// <summary>
/// Reads the categories, items and ready kits of an extracted Store repository. A damaged
/// file skips its category, item or kit instead of the whole catalog.
/// </summary>
internal static class RepositoryCatalogReader
{
    private static readonly List<string> SupportedCategoryTypes = ["basic_category", "second_category"];

    private static T? TryLoad<T>(string path) where T : class
    {
        try
        {
            return JSONConvertor.LoadJson<T>(path);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Skipping \"{path}\": {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Adds the categories of the repository to <paramref name="categories"/> and their items
    /// to <paramref name="items"/>.
    /// </summary>
    public static void LoadCategories(
        string repositoryFolder,
        RepositoryInitializationModel repoInitData,
        List<RepoItemModel> items,
        List<RepoCategoryModel> categories)
    {
        List<string> categoriesAvailable = repoInitData.categories;
        Dictionary<string, string> categoriesPaths = repoInitData.categories_directory;

        List<List<string>> pathsToCheck = new List<List<string>>();

        if (categoriesAvailable != null)
        {
            foreach (string category in categoriesAvailable)
            {
                Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Checking the category: {category}");
                if (!categoriesPaths.ContainsKey(category))
                {
                    Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {category}");
                    continue;
                }

                pathsToCheck.Add([categoriesPaths[category], category]);
            }
        }

        foreach (List<string> _cat in pathsToCheck)
        {
            RepoCategoryModel? repoCategory = LoadCategory(repositoryFolder, _cat[0], _cat[1], items);
            if (repoCategory != null)
                categories.Add(repoCategory);
        }
    }

    private static RepoCategoryModel? LoadCategory(
        string repositoryFolder,
        string categoryPath,
        string categoryName,
        List<RepoItemModel> allItems)
    {
        string categoryInitPath = Path.Combine(repositoryFolder, categoryPath, "init.json");

        if (!Path.Exists(categoryInitPath))
        {
            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {categoryName}, {categoryPath}");
            return null;
        }

        RepoCategoryInitializationModel? repoCategoryInit = TryLoad<RepoCategoryInitializationModel>(categoryInitPath);

        if (repoCategoryInit?.items == null || repoCategoryInit.items_directories == null ||
            repoCategoryInit.type == null || !SupportedCategoryTypes.Contains(repoCategoryInit.type))
        {
            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {categoryName}, {categoryPath}");
            return null;
        }

        List<string> items = repoCategoryInit.items;
        Dictionary<string, string> categoryItemsPaths = repoCategoryInit.items_directories;

        List<string> categoryItemsPathsToCheck = new List<string>();

        foreach (string item in items)
        {
            if (!categoryItemsPaths.ContainsKey(item))
            {
                Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the item: {categoryName}, {categoryPath} >>> {item}");
                continue;
            }

            categoryItemsPathsToCheck.Add(categoryItemsPaths[item]);
        }

        List<RepoItemModel> categoryItems = new List<RepoItemModel>();

        foreach (string categoryItemPath in categoryItemsPathsToCheck)
        {
            string categoryItemInitPath = Path.Combine(repositoryFolder, categoryPath, categoryItemPath, "init.json");
            RepoItemModel? repoCategoryItemInit = LoadItem(categoryItemInitPath, categoryName, categoryPath);
            if (repoCategoryItemInit == null)
                continue;

            repoCategoryItemInit.category_id = repoCategoryInit.store_id;

            allItems.Add(repoCategoryItemInit);
            categoryItems.Add(repoCategoryItemInit);
        }

        RepoCategoryModel repoCategory = new RepoCategoryModel
        {
            store_id = repoCategoryInit.store_id,
            type = repoCategoryInit.type,
            name = categoryName,
            items = categoryItems,
        };
        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Category: {categoryName}, {categoryPath} >>> {repoCategoryInit.store_id}");

        return repoCategory;
    }

    private static RepoItemModel? LoadItem(string categoryItemInitPath, string categoryName, string categoryPath)
    {
        if (!Path.Exists(categoryItemInitPath))
        {
            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the item: {categoryName}, {categoryPath} >>> {categoryItemInitPath}");
            return null;
        }

        RepoItemModel? repoCategoryItemInit = TryLoad<RepoItemModel>(categoryItemInitPath);
        if (repoCategoryItemInit == null)
            return null;
        // The pages look items up by id and show their name; one item without
        // them stopped the whole page from building.
        if (string.IsNullOrWhiteSpace(repoCategoryItemInit.store_id) || string.IsNullOrWhiteSpace(repoCategoryItemInit.name))
        {
            Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Skip the item without store_id or name: {categoryItemInitPath}");
            return null;
        }

        return repoCategoryItemInit;
    }

    public static void LoadReadyKits(string repositoryFolder, string? kitsDirectory, List<ReadyKitModel> readyKits)
    {
        if (string.IsNullOrWhiteSpace(kitsDirectory))
            return;

        string kitsInitPath = Path.Combine(repositoryFolder, kitsDirectory, "init.json");
        if (!File.Exists(kitsInitPath))
            return;

        try
        {
            ReadyKitsInitializationModel? initialization =
                JSONConvertor.LoadJson<ReadyKitsInitializationModel>(kitsInitPath);

            if (initialization?.kits == null || initialization.kits_directories == null)
                return;

            foreach (string kitName in initialization.kits)
            {
                ReadyKitModel? kit = LoadReadyKit(repositoryFolder, kitsDirectory, kitName, initialization.kits_directories);
                if (kit != null)
                    readyKits.Add(kit);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Cannot load ready kits: {ex.Message}");
        }
    }

    private static ReadyKitModel? LoadReadyKit(
        string repositoryFolder,
        string kitsDirectory,
        string kitName,
        Dictionary<string, string> kitsDirectories)
    {
        if (!kitsDirectories.TryGetValue(kitName, out string? kitDirectory) ||
            string.IsNullOrWhiteSpace(kitDirectory))
        {
            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the ready kit: {kitName}");
            return null;
        }

        string kitInitPath = Path.Combine(repositoryFolder, kitsDirectory, kitDirectory, "init.json");
        if (!File.Exists(kitInitPath))
        {
            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the ready kit: {kitName} >>> {kitInitPath}");
            return null;
        }

        // A damaged kit file must not hide the kits after it.
        ReadyKitModel? kit;
        try
        {
            kit = JSONConvertor.LoadJson<ReadyKitModel>(kitInitPath);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Skip the damaged ready kit: {kitName}: {ex.Message}");
            return null;
        }
        if (kit == null || string.IsNullOrWhiteSpace(kit.store_id) || kit.items == null || kit.items.Count == 0)
        {
            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the invalid ready kit: {kitName}");
            return null;
        }

        return kit;
    }
}
