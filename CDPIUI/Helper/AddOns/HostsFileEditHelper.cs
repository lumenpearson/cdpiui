using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.System;

using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CDPIUI.Helper.AddOns;

public class HostsFileEditHelper
{
    public static void EditHostsFile(XamlRoot xamlRoot)
    {
        if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || 
            !SettingsManager.Instance.GetValueOrDefault("FILEOPENACTIONS", "doNotRemindAgain", 
            defaultValue: true))
        {
            // ShowEditAskDialog catches and logs its own errors.
            _ = ShowEditAskDialog(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32/drivers/etc/hosts"), xamlRoot);
        }
        else
        {
            ShellHelper.OpenFile(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32/drivers/etc/hosts"), true, useNotepadAsDefault: true);
        }
    }

    private static async Task ShowEditAskDialog(string file, XamlRoot xamlRoot)
    {
        try
        {
            EditSitelistAskApplicationContentDialog editSitelistAskApplicationContentDialog = new()
            {
                XamlRoot = xamlRoot,
                FilePath = file,
                UseUAC = true,
                UseNotepadAsDefault = true,
            };
            await editSitelistAskApplicationContentDialog.ShowQueuedAsync();
            if (editSitelistAskApplicationContentDialog.IsSuccess)
                SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(HostsFileEditHelper), exception.ToString());
        }
    }
}