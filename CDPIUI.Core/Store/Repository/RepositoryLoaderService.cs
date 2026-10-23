using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Shared.Secrets;
using System.Diagnostics;
using System.IO.Compression;
using CDPIUI.Core.System;
using CDPIUI.Shared.Basic.Filesystem;

namespace CDPIUI.Core.Store.Repository
{
    internal class RepositoryLoaderService: IRepositoryLoaderService
    {
        public SupportedVersionControls VersionControl { get; init; }

        private readonly List<string> SupportedCategoryTypes = ["basic_category", "second_category"];

        private static readonly string GitHubApiToken = Secret.GitHubToken;
        private static readonly string GitLabApiToken = Secret.GitLabToken;

        public event Action<ErrorModel>? InternalErrorHappens;

        public List<RepoCategoryModel>? FormattedStoreDatabase { get; private set; }

        public Dictionary<string, string>? StoreLocalizationPaths { get; private set; }

        public List<RepoItemModel> ItemsList { get; } = [];

        public List<ReadyKitModel> ReadyKits { get; } = [];

        public RepositoryLoaderService()
        {
            VersionControl = 
                SettingsManager.Instance.GetValueOrDefault<string>(
                    "STORE", 
                    "versionControlType", 
                    defaultValue: "GitHub")?
                .ToEnum(SupportedVersionControls.GitHub) ?? SupportedVersionControls.GitHub;
        }

        public string GetToken()
        {
            return VersionControl switch
            {
                SupportedVersionControls.GitHub => GitHubApiToken,
                SupportedVersionControls.GitLab => GitLabApiToken,
                _ => string.Empty,
            };
        }

        public static async Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl)
        {
            try
            {
                string zipUrl = GetStoreUrl(versionControl);

                string tempZipPath = Path.Combine(Path.GetTempPath(), $"cdpiui_store_{Guid.NewGuid():N}.tmp");
                using var requestWorker = new RequestWorker();
                await requestWorker.DownloadFileAsync(zipUrl, tempZipPath,
                    token: versionControl == SupportedVersionControls.GitHub ? GitHubApiToken : GitLabApiToken,
                    useStoreUserAgent: true,
                    completionOption: HttpCompletionOption.ResponseContentRead);

                File.Delete(tempZipPath);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Error loading store database: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> LoadAllStoreDatabase(bool forseSync, SupportedVersionControls versionControl)
        {
            SupportedVersionControls usedVersionControl = versionControl == SupportedVersionControls.None ? VersionControl : versionControl;
            try
            {
                if (FormattedStoreDatabase != null && !forseSync)
                {
                    return true;
                }

                string targetFolder = Directories.StoreRepoCacheDirectory;

                TimeSpan t = DateTime.UtcNow - SettingsManager.Instance.GetValue<DateTime>("STORE", "lastSyncTime");

                if ((forseSync && t.TotalDays >= 1) || !Path.Exists(targetFolder))
                {

                    try
                    {
                        await DownloadAndExtractStore(usedVersionControl, targetFolder);
                    }
                    catch (Exception ex) when (usedVersionControl == SupportedVersionControls.GitHub)
                    {
                        // GitHub is blocked for some users: the GitLab mirror has the same content (issue #26).
                        Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"GitHub is not available, using GitLab: {ex.Message}");
                        await DownloadAndExtractStore(SupportedVersionControls.GitLab, targetFolder);
                    }
                    SettingsManager.Instance.SetValue("STORE", "lastSyncTime", DateTime.UtcNow);
                }

                FormattedStoreDatabase = GetFormattedStoreDatabase();

                return true;
            }
            catch (Exception ex)
            {
                InternalErrorHappens?.Invoke(new ErrorModel()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("STORE_REPO", ex),
                    FriendlyDescription = $"Error loading store database: {ex.Message}",

                    Exception = ex
                });
                Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Error loading store database: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Downloads the Store repository and replaces the cache only after the archive was
        /// extracted completely, so a failed download or a damaged archive keeps the old cache.
        /// </summary>
        private async Task DownloadAndExtractStore(SupportedVersionControls versionControl, string targetFolder)
        {
            string zipUrl = GetStoreUrl(versionControl);
            Debug.WriteLine(zipUrl);

            // A fixed name in the shared temp folder could be prepared by another process.
            string tempZipPath = Path.Combine(Path.GetTempPath(), $"cdpiui_store_{Guid.NewGuid():N}.tmp");
            string newFolder = $"{targetFolder.TrimEnd(Path.DirectorySeparatorChar)}.new-{Guid.NewGuid():N}";
            try
            {
                using (var requestWorker = new RequestWorker())
                {
                    await requestWorker.DownloadFileAsync(zipUrl, tempZipPath,
                        token: versionControl == SupportedVersionControls.GitHub ? GitHubApiToken : GitLabApiToken,
                        useStoreUserAgent: true,
                        completionOption: HttpCompletionOption.ResponseContentRead);
                }

                Directory.CreateDirectory(newFolder);
                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    ArchivePaths.EnsureWithinLimits(archive.Entries);
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                            continue;

                        var parts = entry.FullName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length <= 1)
                            continue;

                        string[] subParts = parts.Skip(1).ToArray();
                        string relativePath = Path.Combine(subParts);

                        string destinationPath = ArchivePaths.GetDestinationOrThrow(newFolder, relativePath);
                        ArchivePaths.EnsureNoReparsePoints(newFolder, destinationPath);

                        string? destDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        ArchivePaths.ExtractToFileBounded(entry, destinationPath);
                    }
                }

                if (Directory.Exists(targetFolder))
                    Directory.Delete(targetFolder, recursive: true);
                Directory.Move(newFolder, targetFolder);
            }
            finally
            {
                try { File.Delete(tempZipPath); }
                catch (IOException ex)
                {
                    Basic.Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Cannot delete the temporary archive {tempZipPath}: {ex.Message}");
                }
                try
                {
                    if (Directory.Exists(newFolder))
                        Directory.Delete(newFolder, recursive: true);
                }
                catch (IOException ex)
                {
                    Basic.Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Cannot delete the temporary folder {newFolder}: {ex.Message}");
                }
            }
        }

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

        public RepoItemModel? GetItemInfoFromStoreId(string? storeId)
        {
            if (storeId == SharedConstants.LocalUserItemsId)
            {
                return null;
            }

            if (storeId == SharedConstants.ApplicationStoreId)
            {
                DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(SharedConstants.ApplicationStoreId);
                RepoItemModel repoCategoryItem = new()
                {
                    store_id = storeId,
                    version_control = "git",
                    version_control_link = VersionControl == SupportedVersionControls.GitHub ? SharedConstants.ApplicationCheckUpdatesUrl : SharedConstants.ApplicationGitLabCheckUpdatesUrl,
                    filetype = State.IsApplicationBuildAsSingleFile ? "CDPIUIUpdateItem" : "UPDmsi",
                    target_executable_file = "patch",
                    developer = databaseStoreItem.Developer,
                    name = databaseStoreItem.Name,
                    short_name = databaseStoreItem.ShortName,
                    icon = databaseStoreItem.IconPath,


                };
                return repoCategoryItem;
            }

            foreach (RepoItemModel repoCategoryItem in ItemsList)
            {
                if (repoCategoryItem.store_id == storeId)
                {
                    return repoCategoryItem;
                }
            }
            return null;
        }

        public RepoCategoryModel? GetCategoryFromStoreId(string storeId)
        {
            foreach (RepoCategoryModel repoCategory in FormattedStoreDatabase)
            {
                if (repoCategory.store_id == storeId)
                {
                    return repoCategory;
                }
            }
            return null;
        }

        public ReadyKitModel? GetReadyKitFromStoreId(string storeId) =>
            ReadyKits.FirstOrDefault(kit => kit.store_id == storeId);

        public static void ClearRepoCache()
        {
            string targetFolder = Directories.StoreRepoCacheDirectory;

            if (Directory.Exists(targetFolder))
                Directory.Delete(targetFolder, recursive: true);
        }


        private List<RepoCategoryModel> GetFormattedStoreDatabase()
        {
            ItemsList?.Clear();
            ReadyKits.Clear();
            List<RepoCategoryModel> categories = new List<RepoCategoryModel>();

            string localAppData = CDPIUI.Core.Data.Directories.DataDirectory;
            string localRepoFolder = Directories.StoreRepoCacheDirectory;
            Debug.WriteLine(localRepoFolder);
            string localRepoInitFile = Path.Combine(localRepoFolder, "init.json");

            if (!File.Exists(localRepoInitFile))
                return categories;

            try
            {
                RepositoryInitializationModel repoInitData = JSONConvertor.LoadJson<RepositoryInitializationModel>(localRepoInitFile);

                StoreLocalizationPaths = repoInitData.localized_strings_directory;

                LoadReadyKits(localRepoFolder, repoInitData.kits_directory);

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
                    string categoryPath = _cat[0];
                    string categoryName = _cat[1];

                    string categoryInitPath = Path.Combine(localRepoFolder, categoryPath, "init.json");

                    if (!Path.Exists(categoryInitPath))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {categoryName}, {categoryPath}");
                        continue;
                    }

                    // One damaged file skips its category or item instead of the whole catalog.
                    RepoCategoryInitializationModel? repoCategoryInit = TryLoad<RepoCategoryInitializationModel>(categoryInitPath);

                    if (repoCategoryInit?.items == null || repoCategoryInit.items_directories == null ||
                        repoCategoryInit.type == null || !SupportedCategoryTypes.Contains(repoCategoryInit.type))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {categoryName}, {categoryPath}");
                        continue;
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
                        string categoryItemInitPath = Path.Combine(localRepoFolder, categoryPath, categoryItemPath, "init.json");

                        if (!Path.Exists(categoryItemInitPath))
                        {
                            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the item: {categoryName}, {categoryPath} >>> {categoryItemInitPath}");
                            continue;
                        }

                        RepoItemModel? repoCategoryItemInit = TryLoad<RepoItemModel>(categoryItemInitPath);
                        if (repoCategoryItemInit == null)
                            continue;
                        // The pages look items up by id and show their name; one item without
                        // them stopped the whole page from building.
                        if (string.IsNullOrWhiteSpace(repoCategoryItemInit.store_id) || string.IsNullOrWhiteSpace(repoCategoryItemInit.name))
                        {
                            Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"Skip the item without store_id or name: {categoryItemInitPath}");
                            continue;
                        }

                        repoCategoryItemInit.category_id = repoCategoryInit.store_id;

                        ItemsList.Add(repoCategoryItemInit);
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

                    categories.Add(repoCategory);
                }

            }
            catch (Exception ex)
            {
                InternalErrorHappens?.Invoke(new ErrorModel()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("STORE_REPO", ex),
                    FriendlyDescription = $"Cannot load Items: {ex.Message}",

                    Exception = ex
                });
            }

            return categories;
        }

        private void LoadReadyKits(string repositoryFolder, string? kitsDirectory)
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
                    if (!initialization.kits_directories.TryGetValue(kitName, out string? kitDirectory) ||
                        string.IsNullOrWhiteSpace(kitDirectory))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the ready kit: {kitName}");
                        continue;
                    }

                    string kitInitPath = Path.Combine(repositoryFolder, kitsDirectory, kitDirectory, "init.json");
                    if (!File.Exists(kitInitPath))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the ready kit: {kitName} >>> {kitInitPath}");
                        continue;
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
                        continue;
                    }
                    if (kit == null || string.IsNullOrWhiteSpace(kit.store_id) || kit.items == null || kit.items.Count == 0)
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the invalid ready kit: {kitName}");
                        continue;
                    }

                    ReadyKits.Add(kit);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Cannot load ready kits: {ex.Message}");
            }
        }

        

        private static string GetStoreUrl(SupportedVersionControls versionControl)
        {
            return string.Format(
                    VersionControlData.VersionControlsLinks[versionControl],
                    versionControl == SupportedVersionControls.GitHub ? SharedConstants.StoreRepo : SharedConstants.GitLabStoreRepo);
        }
    }
}
