using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.BlockCheck2;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUI3Localizer;
using TextDecorations = Windows.UI.Text.TextDecorations;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage
{
    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems.First() is BlockCheck2SiteListItem item)
        {
            ViewModel.SelectSiteList(item);
            SelectSiteListFlyout.Hide();
            InputInfoBar.IsOpen = false;
            UpdateNavigationButtons();

            SiteListBox.SelectedIndex = -1;
        }
    }

    private async void EditCustomSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        SelectSiteListFlyout.Hide();
        await EditCustomSiteListAsync(
            ViewModel.SelectedSiteLists.FirstOrDefault(item => item.IsCustom));
    }

    private async void EditSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BlockCheck2SiteListItem item })
        {
            return;
        }

        if (item.IsCustom)
        {
            await EditCustomSiteListAsync(item);
            return;
        }

        ShellHelper.OpenFileInDefaultApp(item.FilePath);
    }

    private void RemoveSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BlockCheck2SiteListItem item })
        {
            ViewModel.RemoveSiteList(item);
            UpdateNavigationButtons();
        }
    }

    private async Task EditCustomSiteListAsync(
        BlockCheck2SiteListItem? existing)
    {
        CustomSiteListContentDialog dialog = new(existing?.EditableContent)
        {
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await ViewModel.UpdateCustomSiteListAsync(
                dialog.SiteListContent,
                Text("BlockCheck2CustomSiteListName"),
                Text("BlockCheck2LocalSiteListSource"));
            InputInfoBar.IsOpen = false;
            UpdateNavigationButtons();
        }
        catch (Exception exception)
        {
            ShowInputIssue(exception.Message);
        }
    }

    private async void ChooseSiteListFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = Text("BlockCheck2SiteListFileFilter"),
            Multiselect = false,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (!await TryImportSiteListFileAsync(dialog.FileName, dialog.SafeFileName))
            {
                InputInfoBar.Title = Text("BlockCheck2SiteListInvalidTitle");
                InputInfoBar.Message = Text("BlockCheck2SiteListInvalidMessage");
                InputInfoBar.IsOpen = true;
                return;
            }
        }
        catch (Exception exception)
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListsLoadFailedTitle");
            InputInfoBar.Message = exception.Message;
            InputInfoBar.IsOpen = true;
            return;
        }

        SelectSiteListFlyout.Hide();
        InputInfoBar.IsOpen = false;
        UpdateNavigationButtons();
    }

    private void SiteListDropArea_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            SiteListDropBorder.Opacity = 0;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Text("BlockCheck2SiteListDropCaption");
        e.DragUIOverride.IsCaptionVisible = true;
        SiteListDropBorder.Opacity = 1;
        e.Handled = true;
    }

    private void SiteListDropArea_DragLeave(object sender, DragEventArgs e) =>
        SiteListDropBorder.Opacity = 0;

    private async void SiteListDropArea_Drop(object sender, DragEventArgs e)
    {
        SiteListDropBorder.Opacity = 0;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        IReadOnlyList<IStorageItem> droppedItems;
        try
        {
            droppedItems = await e.DataView.GetStorageItemsAsync();
        }
        catch (Exception exception)
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListsLoadFailedTitle");
            InputInfoBar.Message = exception.Message;
            InputInfoBar.IsOpen = true;
            return;
        }
        int imported = 0;
        int skipped = 0;
        List<string> errors = [];
        foreach (IStorageItem droppedItem in droppedItems)
        {
            if (droppedItem is not StorageFile file ||
                !string.Equals(file.FileType, ".txt", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(file.Path))
            {
                skipped++;
                continue;
            }

            try
            {
                if (await TryImportSiteListFileAsync(file.Path, file.Name))
                {
                    imported++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception exception)
            {
                errors.Add($"{file.Name}: {exception.Message}");
            }
        }

        UpdateNavigationButtons();
        if (skipped > 0 || errors.Count > 0)
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListDropPartialTitle");
            InputInfoBar.Message = string.Format(
                Text("BlockCheck2SiteListDropPartialFormat"),
                imported,
                skipped + errors.Count,
                errors.Count == 0
                    ? string.Empty
                    : Environment.NewLine + string.Join(Environment.NewLine, errors.Take(3)));
            InputInfoBar.IsOpen = true;
        }
        else
        {
            InputInfoBar.IsOpen = false;
        }
    }

    private async Task<bool> TryImportSiteListFileAsync(
        string filePath,
        string displayName)
    {
        string? storedPath = await BlockCheck2SiteListImporter.TryImportAsync(filePath, displayName);
        if (storedPath == null)
        {
            return false;
        }

        BlockCheck2SiteListItem item = ViewModel.AddCustomSiteList(
            storedPath,
            Text("BlockCheck2LocalSiteListSource"));
        ViewModel.SelectSiteList(item);
        return true;
    }
}
