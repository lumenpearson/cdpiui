using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Core.Store;

/// <summary>
/// File operations on the folders of installed Store items.
/// </summary>
internal static class StoreItemFolders
{
    /// <summary>
    /// Deletes the folder of an item, retrying while its files are still in use.
    /// </summary>
    public static async Task DeleteItemDirectoryAsync(string directory)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 5)
                {
                    Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"Cannot delete \"{directory}\": {ex.Message}");
                    break;
                }
                await Task.Delay(500 * attempt);
            }
        }
    }

    /// <summary>
    /// RemoveItemAsync works from the database record, which a failed first installation does
    /// not have yet; its downloaded files stayed in the item folder.
    /// </summary>
    public static void DeleteFolderWithoutRecord(string itemId)
    {
        if (!StoreItemIds.IsValid(itemId) ||
            itemId == SharedConstants.ApplicationStoreId ||
            itemId == SharedConstants.LocalUserItemsId ||
            DatabaseHelper.Instance.GetItemById(itemId) != null)
            return;

        string itemFolder = Path.Combine(Directories.StoreItemsDirectory, itemId);
        if (!Directory.Exists(itemFolder) || !StoreItemIds.IsItemFolder(itemFolder))
            return;
        try
        {
            Directory.Delete(itemFolder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Cannot delete \"{itemFolder}\": {ex.Message}");
        }
    }

    // A local pack names its icon as $DYNAMICIMAGE(icon.png) relative to the pack. Evaluated later
    // without the pack folder, the path pointed into the repository cache, so the icon of a local
    // pack is stored as a full path, and only when the file is inside the item folder.
    public static string? GetIconPath(RepoItemModel item, string itemFolder)
    {
        const string prefix = "$DYNAMICIMAGE(";
        string? icon = item.icon;
        if (item.filetype != "localPack" || icon == null ||
            !icon.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !icon.EndsWith(')'))
            return icon;

        try
        {
            string root = Path.GetFullPath(itemFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(itemFolder, icon[prefix.Length..^1]));
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : icon;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return icon;
        }
    }

    public static OperationResultModel<EmptyResult> CreateItemDirectory(string itemId, string itemFolder, bool clean)
    {
        if (itemId == SharedConstants.ApplicationStoreId || itemId == SharedConstants.LocalUserItemsId)
            clean = false;

        try
        {
            if (Path.Exists(itemFolder) && !string.Equals(itemFolder, Directories.DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (clean)
                    Directory.Delete(itemFolder, recursive: true);
            }
            Directory.CreateDirectory(itemFolder);
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(StoreInstallErrors.FromException(ex)));
        }

        return OperationResultModel<EmptyResult>
            .SuccessResult();
    }

    public static OperationResultModel<EmptyResult> CopyLocalPack(string itemId, string itemFolder, string packFilePath)
    {
        try
        {
            if (itemId != SharedConstants.ApplicationStoreId)
            {
                // The installed version is replaced only by a pack that can be installed.
                if (!File.Exists(Path.Combine(packFilePath, "init.json")))
                    return OperationResultModel<EmptyResult>
                        .FailureResult(StoreInstallErrors.FromCode(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

                ReplaceItemFolder(itemFolder, packFilePath);
            }
            else
            {
                File.Copy(packFilePath, Path.Combine(itemFolder, "patch.cdpipatch"), overwrite: true);
            }
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>
                .FailureResult(ErrorModel.OnlyErrorCode(StoreInstallErrors.FromException(ex)));
        }

        return OperationResultModel<EmptyResult>
            .SuccessResult();
    }

    // The previous version is restored when the new folder cannot be moved in.
    private static void ReplaceItemFolder(string itemFolder, string packFilePath)
    {
        string? previous = null;
        if (Directory.Exists(itemFolder))
        {
            previous = $"{itemFolder}.previous-{Guid.NewGuid():N}";
            Directory.Move(itemFolder, previous);
        }
        try
        {
            Directory.Move(packFilePath, itemFolder);
        }
        catch
        {
            if (previous != null && !Directory.Exists(itemFolder))
                Directory.Move(previous, itemFolder);
            throw;
        }
        if (previous != null)
        {
            try
            { Directory.Delete(previous, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Instance.CreateWarningLog(nameof(StoreHelper), $"Cannot delete the previous version \"{previous}\": {ex.Message}");
            }
        }
    }
}
