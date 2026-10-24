using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Controls.Store;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Default;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Helper.Migration;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.ViewModels;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;


namespace CDPIUI;

public sealed partial class WelcomeWindow
{
    private async Task DownloadItems()
    {
        try
        {
            foreach (StoreViewBundleItem kit in StoreViewBundles.SelectedItems.Cast<StoreViewBundleItem>())
            {
                var _kit = StoreHelper.Instance.GetReadyKitFromStoreId(kit.KitId);
                if (_kit == null)
                    continue;

                var _items = (_kit.items ?? [])
                    .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
                    .OfType<RepoItemModel>()
                    .ToList();

                List<RepoItemModel> itemsToInstall = _items
                    .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id))
                    .ToList();
                if (itemsToInstall.Count == 0)
                    continue;

                List<ItemLicenseModel> licenses = StoreItemLicenses.CollectDistinct(itemsToInstall);

                if (licenses.Count > 0)
                {
                    AcceptLicenseContentDialog dialog = new()
                    {
                        Licenses = licenses,
                        XamlRoot = ContentRoot.XamlRoot
                    };
                    if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
                        return;
                }

                HashSet<string> pendingIds = itemsToInstall
                    .Select(item => item.store_id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string itemId in _kit.items ?? [])
                {
                    if (pendingIds.Contains(itemId))
                        StoreHelper.Instance.AddItemToQueue(itemId, string.Empty);
                }
            }

        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }

    private async Task TryLoadStore()
    {
        try
        {
            ErrorStackPanel.Visibility = Visibility.Collapsed;
            StoreViewBundles.Visibility = Visibility.Collapsed;

            UtilityButtonControls.SetButtonVisibilities(
                (NextButton, Visibility.Collapsed),
                (SkipButton, Visibility.Visible));
            UtilityButtonControls.IsLoading = true;
            UtilityButtonControls.LoadingStateText = _localizer.GetLocalizedString("UpdatingStoreDatabase");

            bool result = await StoreHelper.Instance.LoadAllStoreDatabase();

            if (result)
                result = LoadReadyKits();

            ErrorStackPanel.Visibility = result ? Visibility.Collapsed : Visibility.Visible;
            StoreViewBundles.Visibility = result ? Visibility.Visible : Visibility.Collapsed;

            if (AnimatedHorizontalContentViewer.SelectedItem == StoreItem)
            {
                UtilityButtonControls.IsLoading = false;
                UtilityButtonControls.SetButtonVisibilities(
                    (NextButton, Visibility.Collapsed),
                    (SkipButton, Visibility.Visible));
            }
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }

    private bool LoadReadyKits()
    {
        Kits.Clear();
        List<ReadyKitModel> kits = StoreHelper.Instance.ReadyKits
            .OrderByDescending(kit => kit.IsRecommended)
            .ToList();

        if (kits.Count == 0)
        {
            Kits.Clear();
            return false;
        }
        foreach (var kit in kits.Select(ReadyKitViewModelFactory.Create))
            Kits.Add(kit);

        return true;
    }

    private void StoreViewBundles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UtilityButtonControls.SetButtonVisibilities(
            (NextButton, StoreViewBundles.SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed),
            (SkipButton, Visibility.Visible));
    }

    private void CheckStoreStatusButton_Click(object sender, RoutedEventArgs e)
    {
        _ = CheckStoreStatus(); // CheckStoreStatus catches and logs its own errors.
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _ = CheckStoreStatus(); // CheckStoreStatus catches and logs its own errors.
    }

    private async Task CheckStoreStatus()
    {
        try
        {
            CheckStoreStatusButton.IsEnabled = false;
            UtilityButtonControls.IsLoading = true;
            await Task.Delay(500);
            bool result = await StoreHelper.Instance.IsServerAvailable();
            UtilityButtonControls.IsLoading = false;
            CheckStoreStatusButton.IsEnabled = true;
            StoreStatusTextBlock.Text = result ? _localizer.GetLocalizedString("Available") : _localizer.GetLocalizedString("NotAvailable");
            StoreStatusTextBlock.Foreground = result
                ? (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"]
                : (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
        }
    }
}
