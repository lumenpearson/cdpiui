using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Core.Store;

/// <summary>
/// Moves a local pack into its item folder and describes it as a Store item.
/// </summary>
internal static class LocalPackItemReader
{
    /// <summary>
    /// Returns the version, the download URL (none for a local pack) and the item.
    /// </summary>
    /// <param name="getItemInfo">Finds the catalog entry of the application update.</param>
    public static OperationResultModel<Tuple<string?, string?, RepoItemModel>> Prepare(
        QueueItemModel qi,
        Func<string, RepoItemModel?> getItemInfo)
    {
        RepoItemModel item;
        string? tag;
        string downloadUrl;

        string id = qi.ItemId;
        string packFilePath = qi.PackFilePath!;

        string itemFolder = Path.Combine(Directories.StoreItemsDirectory, qi.ItemId);

        var copyResult = StoreItemFolders.CopyLocalPack(id, itemFolder, packFilePath);
        if (!copyResult.Success)
            return OperationResultModel<Tuple<string?, string?, RepoItemModel>>.FailureResult(copyResult.Error);

        if (qi.ItemId == SharedConstants.ApplicationStoreId)
        {
            item = getItemInfo(qi.ItemId);
            tag = string.Empty;
            downloadUrl = null;
        }
        else
        {
            string initFilePath = Path.Combine(itemFolder, "init.json");

            if (!File.Exists(initFilePath))
                return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
                    .FailureResult(StoreInstallErrors.FromCode(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

            LocalItemInitModel? localItemInitModel = ReadInitFile(initFilePath);
            if (localItemInitModel == null)
                return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
                    .FailureResult(StoreInstallErrors.FromCode(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

            item = CreateItem(qi, localItemInitModel);
            tag = localItemInitModel.Version;
            downloadUrl = null;
        }

        if (item == null)
            return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
                .FailureResult(StoreInstallErrors.FromCode(PrettyErrorCode.NULL_REFERENCE));

        return OperationResultModel<Tuple<string?, string?, RepoItemModel>>
            .SuccessResult(Tuple.Create(tag, downloadUrl, item));
    }

    private static LocalItemInitModel? ReadInitFile(string initFilePath)
    {
        try
        {
            return JSONConvertor.LoadJson<LocalItemInitModel>(initFilePath);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"Cannot read \"{initFilePath}\": {ex.Message}");
            return null;
        }
    }

    private static RepoItemModel CreateItem(QueueItemModel qi, LocalItemInitModel localItemInitModel)
    {
        return new()
        {
            store_id = qi.ItemId,
            type = localItemInitModel.Type ?? "configlist",
            name = localItemInitModel.Name ?? localItemInitModel.ShortName ?? qi.ItemId,
            short_name = localItemInitModel.ShortName ?? qi.ItemId,
            // An unsigned pack is only a set of presets and lists: it cannot name the executable
            // the tray starts elevated, and it cannot run install actions.
            target_executable_file = qi.IsSignedPack ? localItemInitModel.ExecutableFile : null,
            filetype = "localPack",
            version_control = "local",
            icon = localItemInitModel.Icon,
            developer = localItemInitModel.Developer,
            background = localItemInitModel.Color,
            dependencies = localItemInitModel.Requirements,
            before_install_actions = qi.IsSignedPack ? localItemInitModel.BeforeInstallActions : null,
            after_install_actions = qi.IsSignedPack ? localItemInitModel.AfterInstallActions : null,
        };
    }
}
