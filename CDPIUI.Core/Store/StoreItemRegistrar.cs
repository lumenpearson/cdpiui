using CDPIUI.Core.Basic;
using CDPIUI.Core.LScript;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Core.Store;

/// <summary>
/// Runs the install actions of a downloaded item and records it in the item database.
/// </summary>
internal sealed class StoreItemRegistrar
{
    private readonly OnlineItemsInstallationService _onlineItemsInstallationService;

    public StoreItemRegistrar(OnlineItemsInstallationService onlineItemsInstallationService)
    {
        _onlineItemsInstallationService = onlineItemsInstallationService;
    }

    public async Task<OperationResultModel<EmptyResult>> AddItemToDatabase(
        string? version,
        string? downloadUrl,
        string itemFolder,
        RepoItemModel item,
        List<Tuple<string, string>>? requiredItems,
        CancellationToken? cancellationToken)
    {
        DatabaseStoreItem databaseStoreItem = CreateDatabaseItem(version, downloadUrl, itemFolder, item, requiredItems);

        // Both action lists run with the item folder: install_msi of a "before" action had none
        // and always failed.
        Dictionary<string, string> extraArgs = new()
        {
            { "CurrentDirectory", itemFolder }
        };

        try
        {
            if (!await _onlineItemsInstallationService.LaunchBeforeInstallActions(
                    await LScriptCore.RunScript(item.before_install_actions, extraArgs: extraArgs, cancellationToken: cancellationToken),
                    itemFolder))
                return OperationResultModel<EmptyResult>
                    .UnSuccessResult();

            AddDependencies(item.dependencies, item.store_id!);

            // TODO: add RequiredItemIds to new installed item from store (foreach)

            Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Adding item {item.name} to database...");
            if (!DatabaseHelper.Instance.AddOrUpdateItem(databaseStoreItem))
                return OperationResultModel<EmptyResult>
                    .FailureResult(ErrorModel.OnlyErrorCode(StoreInstallErrors.FromException(new IOException($"Cannot save {item.store_id} to the item database."))));

            // download_easy_designer_annotation_file returns a download request; it was dropped.
            string? afterDownloads = await LScriptCore.RunScript(item.after_install_actions, extraArgs: extraArgs, cancellationToken: cancellationToken);
            if (!await _onlineItemsInstallationService.LaunchBeforeInstallActions(afterDownloads, itemFolder))
                Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Downloads after the installation of {item.store_id} failed.");

            Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Adding item {item.name}... COMPLETE");

            return OperationResultModel<EmptyResult>.SuccessResult();
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(StoreInstallErrors.FromException(ex)));
        }
    }

    private static DatabaseStoreItem CreateDatabaseItem(
        string? version,
        string? downloadUrl,
        string itemFolder,
        RepoItemModel item,
        List<Tuple<string, string>>? requiredItems)
    {
        List<Tuple<string, string>> _dependencies = [];

        // A one-element entry in init.json threw here and failed the installation silently.
        foreach (string[] dependency in item.dependencies ?? [])
        {
            if (dependency is { Length: >= 2 })
                _dependencies.Add(Tuple.Create(dependency[0], dependency[1]));
        }

        return new()
        {
            Id = item.store_id,
            Type = item.type,
            Name = item.name,
            ShortName = item.short_name,
            CurrentVersion = version,
            Directory = itemFolder,
            Executable = item.target_executable_file,
            DownloadUrl = downloadUrl,
            DownloadFileType = item.filetype,
            IconPath = StoreItemFolders.GetIconPath(item, itemFolder),
            UpdateCheckUrl = item.version_control_link,
            VersionControlType = item.version_control,
            DependentItemIds = _dependencies,
            RequiredItemIds = requiredItems,
            Developer = item.developer,
            BackgroundColor = item.background,
        };
    }

    private static void AddDependencies(List<string[]>? dependencies, string id)
    {
        foreach (var dependency in dependencies ?? [])
        {
            if (dependency.Length < 2 || !DatabaseHelper.Instance.IsItemInstalled(dependency[0]))
                continue;

            DatabaseStoreItem dependencyItem = DatabaseHelper.Instance.GetItemById(dependency[0]);
            if (dependencyItem == null)
                continue;
            // A reinstall added the same entry again every time.
            dependencyItem.RequiredItemIds ??= [];
            if (dependencyItem.RequiredItemIds.Any(r => r.Item1 == id))
                continue;
            dependencyItem.RequiredItemIds.Add(Tuple.Create(id, dependency[1]));
            DatabaseHelper.Instance.AddOrUpdateItem(dependencyItem);
        }
    }
}
