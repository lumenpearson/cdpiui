using System;
using System.Windows.Forms;
using WinUI3Localizer;

namespace CDPIUI.Helper.CreateConfigHelper;

// The file pickers of the config helper window. Each returns null when the user cancels.
internal static class CreateConfigHelperFileDialogs
{
    public static string ChooseConfigFile(ILocalizer localizer)
    {
        using OpenFileDialog openFileDialog = new();
        openFileDialog.Title = localizer.GetLocalizedString("FileDialogChooseConfigTitle");
        openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        openFileDialog.FilterIndex = 4;

        openFileDialog.Filter = $"{localizer.GetLocalizedString("FileDialogJsonConfigs")} (*.json)|*.json|{localizer.GetLocalizedString("FileDialogBatConfigs")} (*.bat)|*.bat|{localizer.GetLocalizedString("FileDialogCmdConfigs")} (*.cmd)|*.cmd|{localizer.GetLocalizedString("FileDialogAllConfigs")} (*.bat, *.cmd, *.json)|*.bat;*.cmd;*.json";
        openFileDialog.RestoreDirectory = true;

        return openFileDialog.ShowDialog() == DialogResult.OK
            ? openFileDialog.FileName
            : null;
    }

    public static string ChooseGoodCheckReport(ILocalizer localizer)
    {
        using OpenFileDialog openFileDialog = new();
        openFileDialog.Title = localizer.GetLocalizedString("FileDialogChooseGoodCheckReportTitle");
        openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        openFileDialog.Filter = $"{localizer.GetLocalizedString("FileDialogXmlData")} (*.xml)|*.xml";
        openFileDialog.RestoreDirectory = true;

        return openFileDialog.ShowDialog() == DialogResult.OK
            ? openFileDialog.FileName
            : null;
    }
}
