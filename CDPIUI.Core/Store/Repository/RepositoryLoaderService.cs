using System.Diagnostics;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Core.Store.Repository;

internal class RepositoryLoaderService : IRepositoryLoaderService
{
    public SupportedVersionControls VersionControl { get; init; }

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

    public string GetToken() => StoreRepositoryDownloader.GetToken(VersionControl);

    public static Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl) =>
        StoreRepositoryDownloader.TryLoadDatabaseForVersionControl(versionControl);

    private readonly StoreLoadGate<(bool ForseSync, SupportedVersionControls VersionControl)> _loadGate = new();

    public Task<bool> LoadAllStoreDatabase(bool forseSync, SupportedVersionControls versionControl)
    {
        SupportedVersionControls usedVersionControl = versionControl == SupportedVersionControls.None ? VersionControl : versionControl;

        // Several Store buttons ask for the database at the same time on the first launch:
        // they share one download.
        return _loadGate.Run(
            (forseSync, usedVersionControl),
            () => LoadAllStoreDatabaseCore(forseSync, usedVersionControl));
    }

    private async Task<bool> LoadAllStoreDatabaseCore(bool forseSync, SupportedVersionControls usedVersionControl)
    {
        try
        {
            // The parsed catalogue is kept only while its folder exists: icons and item pages
            // are read from there, and the Store cache clean-up deletes it.
            if (FormattedStoreDatabase != null && !forseSync && Directory.Exists(Directories.StoreRepoCacheDirectory))
            {
                return true;
            }

            string targetFolder = Directories.StoreRepoCacheDirectory;

            TimeSpan t = DateTime.UtcNow - SettingsManager.Instance.GetValue<DateTime>("STORE", "lastSyncTime");

            if ((forseSync && t.TotalDays >= 1) || !Path.Exists(targetFolder))
            {

                try
                {
                    await StoreRepositoryDownloader.DownloadAndExtractStore(usedVersionControl, targetFolder);
                }
                catch (Exception ex) when (usedVersionControl == SupportedVersionControls.GitHub)
                {
                    // GitHub is blocked for some users: the GitLab mirror has the same content (issue #26).
                    Logger.Instance.CreateWarningLog(nameof(RepositoryLoaderService), $"GitHub is not available, using GitLab: {ex.Message}");
                    await StoreRepositoryDownloader.DownloadAndExtractStore(SupportedVersionControls.GitLab, targetFolder);
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
        ItemsList.Clear();
        ReadyKits.Clear();
        List<RepoCategoryModel> categories = new List<RepoCategoryModel>();

        string localRepoFolder = Directories.StoreRepoCacheDirectory;
        Debug.WriteLine(localRepoFolder);
        string localRepoInitFile = Path.Combine(localRepoFolder, "init.json");

        if (!File.Exists(localRepoInitFile))
            return categories;

        try
        {
            // An empty file or "null" gives no model.
            RepositoryInitializationModel repoInitData = JSONConvertor.LoadJson<RepositoryInitializationModel>(localRepoInitFile)
                ?? throw new UnknownFileFormatException();

            StoreLocalizationPaths = repoInitData.localized_strings_directory;

            RepositoryCatalogReader.LoadReadyKits(localRepoFolder, repoInitData.kits_directory, ReadyKits);
            RepositoryCatalogReader.LoadCategories(localRepoFolder, repoInitData, ItemsList, categories);
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
}
