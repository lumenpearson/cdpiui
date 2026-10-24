using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class ResultPage
{
    private readonly BlockCheckReportSerializer _reportSerializer = new();

    public void SaveJsonReportFromMenu() => SaveJsonReportButton_Click(this, new RoutedEventArgs());

    public void SaveTextReportFromMenu() => SaveTextReportButton_Click(this, new RoutedEventArgs());

    private async void SaveJsonReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Report == null)
        {
            return;
        }
        SaveFileDialog dialog = CreateSaveDialog(
            Text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.json",
            Text("BlockCheck2JsonFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            await SaveAsync(() => _reportSerializer.SaveJsonAsync(dialog.FileName, ViewModel.Report));
        }
    }

    private async void SaveTextReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Report == null)
        {
            return;
        }
        SaveFileDialog dialog = CreateSaveDialog(
            Text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.txt",
            Text("BlockCheck2TextFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            await SaveAsync(() => _reportSerializer.SaveTextAsync(dialog.FileName, ViewModel.Report));
        }
    }

    private async Task SaveAsync(Func<Task> saveAction)
    {
        try
        {
            await saveAction();
            ShowActionMessage(
                InfoBarSeverity.Success,
                Text("BlockCheck2ActionCompletedTitle"),
                Text("BlockCheck2FileSavedMessage"));
        }
        catch (Exception exception)
        {
            ShowActionError(exception.Message);
        }
    }

    private static SaveFileDialog CreateSaveDialog(string title, string fileName, string filter) => new()
    {
        Title = title,
        FileName = fileName,
        Filter = filter,
        OverwritePrompt = true,
        RestoreDirectory = true,
    };

    private void CopyViewAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }

        Copy(item.Evidence.StrategyArguments);
    }

    private void CopyFullAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }
        var result = ViewModel.GetFullStrategyArguments(item);
        if (result.Success)
            Copy(result.Result ?? string.Empty);
    }

    private void Copy(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);

        ShowActionMessage(InfoBarSeverity.Informational, Text("CopyComplete"), string.Empty);
    }
}
