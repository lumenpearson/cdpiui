using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.BlockCheck2;
using CDPIUI.Shared;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage
{
    private readonly ILocalizer _localizer = Localizer.Get();

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems.First() is BlockCheck2SiteListItem item)
        {
            SelectSiteListFlyout.Hide();
            ViewModel.SelectSiteList(item);

            SiteListBox.SelectedIndex = -1;
        }
    }

    private async void EditCustomSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        SelectSiteListFlyout.Hide();
        await EditCustomSiteListAsync(
            ViewModel.Session.SelectedSiteLists.FirstOrDefault(item => item.IsCustom));
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

        ViewModel.OpenSiteListFile(item);
    }

    private void RemoveSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BlockCheck2SiteListItem item })
        {
            ViewModel.RemoveSiteList(item);
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

        await ViewModel.ApplyCustomSiteListAsync(dialog.SiteListContent);
    }

    private async void ChooseSiteListFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = _localizer.GetLocalizedString("BlockCheck2SiteListFileFilter"),
            Multiselect = false,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (await ViewModel.ImportSiteListFileAsync(dialog.FileName, dialog.SafeFileName))
        {
            SelectSiteListFlyout.Hide();
        }
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
        e.DragUIOverride.Caption = _localizer.GetLocalizedString("BlockCheck2SiteListDropCaption");
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
            ViewModel.ShowSiteListLoadFailed(exception.Message);
            return;
        }

        List<(string Path, string Name)> files = [];
        int skipped = 0;
        foreach (IStorageItem droppedItem in droppedItems)
        {
            if (droppedItem is not StorageFile file ||
                !string.Equals(file.FileType, ".txt", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(file.Path))
            {
                skipped++;
                continue;
            }

            files.Add((file.Path, file.Name));
        }

        await ViewModel.ImportDroppedSiteListsAsync(files, skipped);
    }
}
