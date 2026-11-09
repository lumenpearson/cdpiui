using System.Diagnostics;
using System.Runtime.InteropServices;
using CDPIUI.Core.Basic;
using CDPIUI.Shared.Models;

namespace CDPIUI.Core.Features;

public static class FileSystemLinksManager
{
    public static string? IsFileLinked(string itemId, string fileName)
    {
        try
        {
            string filePath = SettingsManager.Instance.GetValue<string>(
                ["HARDLINKS", itemId, fileName], "targetFile", raiseExceptionIfNotExits: true);
            return filePath == "NaN" ? null : filePath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<OperationResultModel<EmptyResult>> CreateSymbolicLinkForItemId(
        string itemId,
        string linkFrom,
        string linkTo)
    {
        try
        {
            ReplaceWithLink(itemId, linkTo, linkFrom, symbolic: true, temporaryPath => File.CreateSymbolicLink(temporaryPath, linkFrom));
            SettingsManager.Instance.SetValue(
                ["HARDLINKS", itemId, linkTo], "targetFile", linkFrom);

            return OperationResultModel<EmptyResult>.SuccessResult();
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>.FailureResult(new()
            {
                ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("SOFTLINK", ex),
                FriendlyDescription = ex.Message,
            });
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public static async Task<OperationResultModel<EmptyResult>> RemoveLinkForItemId(
        string itemId,
        string linkFrom,
        string linkTo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(linkFrom)) throw new ArgumentException("A recorded link target is required.", nameof(linkFrom));
            Debug.WriteLine($">>> {linkTo} {itemId}");
            string? backupFile = SettingsManager.Instance.GetValue<string>(
                ["HARDLINKS", itemId, linkTo], "backupFile");
            bool hadOriginal = !string.IsNullOrEmpty(backupFile) && backupFile != "NaN";
            LinkFileTransaction.Restore(linkTo, hadOriginal ? backupFile : null, linkFrom);

            SettingsManager.Instance.SetValue(["HARDLINKS", itemId, linkTo], "backupFile", "NaN");
            SettingsManager.Instance.SetValue(["HARDLINKS", itemId, linkTo], "targetFile", "NaN");

            return OperationResultModel<EmptyResult>.SuccessResult();
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>.FailureResult(new()
            {
                ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("SOFTLINK", ex),
                FriendlyDescription = ex.Message,
            });
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public static async Task<OperationResultModel<EmptyResult>> CreateHardLinkForItemId(
        string itemId,
        string linkFrom,
        string linkTo)
    {
        try
        {
            ReplaceWithLink(itemId, linkTo, linkFrom, symbolic: false, temporaryPath =>
            {
                // Fails, for example, between two volumes; that was reported as success.
                if (!CreateHardLink(temporaryPath, linkFrom, nint.Zero))
                    throw new global::System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            });
            SettingsManager.Instance.SetValue(
                ["HARDLINKS", itemId, linkTo], "targetFile", linkFrom);

            return OperationResultModel<EmptyResult>.SuccessResult();
        }
        catch (Exception ex)
        {
            return OperationResultModel<EmptyResult>.FailureResult(new()
            {
                ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("SOFTLINK", ex),
                FriendlyDescription = ex.Message,
            });
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Creates the link under a temporary name first and replaces the file only when that
    /// worked: the original was deleted before the link was created, so a failed
    /// CreateSymbolicLink (no privilege without developer mode) left nothing in its place.
    /// </summary>
    private static void ReplaceWithLink(string itemId, string linkTo, string linkFrom, bool symbolic, Action<string> createLink)
    {
        if (string.IsNullOrWhiteSpace(linkFrom)) throw new ArgumentException("A link target is required.", nameof(linkFrom));
        string? previousTarget = IsFileLinked(itemId, linkTo);
        string? backup = LinkFileTransaction.Replace(linkTo, previousTarget != null, createLink, linkFrom, symbolic, previousTarget);
        if (backup != null)
            SettingsManager.Instance.SetValue(["HARDLINKS", itemId, linkTo], "backupFile", backup);
    }

    #region WINAPI

    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink
    (
        string lpFileName,
        string lpExistingFileName,
        nint lpSecurityAttributes
    );

    enum SymbolicLink
    {
        File = 0,
        Directory = 1
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CreateSymbolicLink(
        string lpSymlinkFileName,
        string lpTargetFileName,
        SymbolicLink dwFlags
    );



    #endregion
}
