using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Store;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using DependencyObject = Microsoft.UI.Xaml.DependencyObject;
using ItemsControl = Microsoft.UI.Xaml.Controls.ItemsControl;
using ItemsPanelTemplate = Microsoft.UI.Xaml.Controls.ItemsPanelTemplate;
using SizeChangedEventArgs = Microsoft.UI.Xaml.SizeChangedEventArgs;
using Thickness = Microsoft.UI.Xaml.Thickness;
using UIElement = Microsoft.UI.Xaml.UIElement;
using Visibility = Microsoft.UI.Xaml.Visibility;
using Window = Microsoft.UI.Xaml.Window;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HomePage : TemplatePage
{
    private readonly List<(ItemsControl Panel, UICategoryData Data)> _panels
        = new List<(ItemsControl, UICategoryData)>();

    private double _largeMinW, _largePrefW;

    private const int DefaultLargeColumns = 8;
    private const double MaxTotalWidth = 1860;

    private IEnumerable<UICategoryData> _lastCategories;

    private ILocalizer _localizer = Localizer.Get();

    // Database update started event will be removed.

    public HomePage()
    {
        InitializeComponent();

        StoreViewBundles.BundleClick += ReadyKitButton_Click;
        BundlesCategoryButton.Click += BundlesCategoryButton_Click;

        // SetupLayout();

        StoreHelper.Instance.StoreInternalErrorHappens += StoreHelper_ErrorHappens;
        _ = UpdateStoreDatabase(); // UpdateStoreDatabase catches and logs its own errors.

        StoreScrollViewer.SizeChanged += OnContainerSizeChanged;

        this.Loaded += HomePage_Loaded;

        LargeElementWidth = 100;
        this.DataContext = this;
        this.LayoutUpdated += HomePage_LayoutUpdated;

    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        StoreHelper.Instance.StoreInternalErrorHappens -= StoreHelper_ErrorHappens;
        this.LayoutUpdated -= HomePage_LayoutUpdated;
        StoreScrollViewer.SizeChanged -= OnContainerSizeChanged;

        foreach (var cat in _lastCategories ?? Enumerable.Empty<UICategoryData>())
        {
            foreach (var ui in cat.Items)
            {
                if (ui is StoreItemLargeButton sb)
                {
                    sb.Click -= StoreItemButton_Click;
                    sb.Tag = null;
                    sb.CardImageSource = null;
                }
                else if (ui is StoreItemSmallButton smb)
                {
                    smb.Click -= StoreItemButton_Click;
                    smb.Tag = null;
                    smb.CardImageSource = null;
                }
            }
        }

        _panels.Clear();
        StoreViewBundles.ClearBundles();
        _lastCategories = null;

        StoreStackPanel.Children.Clear();
    }


    private void HomePage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {


    }

    private List<UICategoryData> CreateCategoriesList(List<RepoCategoryModel> values)
    {
        List<UICategoryData> categoryDatas = new List<UICategoryData>();
        // Ids are compared as the database compares them.
        HashSet<string> installedIds = DatabaseHelper.Instance.GetAllInstalledItems()
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (RepoCategoryModel category in values)
        {
            ObservableCollection<UIElement> categoryItems = new ObservableCollection<UIElement>();
            foreach (RepoItemModel repoCategoryItem in category.items)
            {
                if (category.type == "basic_category")
                {
                    categoryItems.Add(
                        UIHelper.CreateLargeButton(
                            storeId: repoCategoryItem.store_id,
                            imageSource: LScriptLangHelper.ExecuteScript(repoCategoryItem.icon),
                            price: installedIds.Contains(repoCategoryItem.store_id) ? _localizer.GetLocalizedString("Installed") : _localizer.GetLocalizedString("Get"),
                            title: StoreHelper.Instance.GetLocalizedStoreItemName(repoCategoryItem.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                            backgroundColor: repoCategoryItem.background,
                            action: StoreItemButton_Click
                        )
                    );
                }
                else if (category.type == "second_category")
                {
                    categoryItems.Add(
                        UIHelper.CreateSmallButton(
                            storeId: repoCategoryItem.store_id,
                            imageSource: LScriptLangHelper.ExecuteScript(repoCategoryItem.icon),
                            price: installedIds.Contains(repoCategoryItem.store_id) ? _localizer.GetLocalizedString("Installed") : _localizer.GetLocalizedString("Get"),
                            title: StoreHelper.Instance.GetLocalizedStoreItemName(repoCategoryItem.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                            developer: repoCategoryItem.developer,
                            backgroundColor: repoCategoryItem.background,
                            action: StoreItemButton_Click
                        )
                    );
                }
            }

            UICategoryData categoryData = new UICategoryData
            {
                StoreId = category.store_id,
                Type = category.type,
                Name = StoreHelper.Instance.GetLocalizedStoreItemName(category.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                Items = categoryItems,
            };

            categoryDatas.Add(categoryData);
        }

        return categoryDatas;
    }

    public void InitializeAndShow(IEnumerable<UICategoryData> categories)
    {
        _lastCategories = categories;
        LoadCategories(categories);
    }

    private void LoadCategories(IEnumerable<UICategoryData> categories)
    {
        _panels.Clear();
        StoreStackPanel.Children.Clear();

        double totalWidth = Math.Min(StoreScrollViewer.ActualWidth
                                      - StoreScrollViewer.Padding.Left
                                      - StoreScrollViewer.Padding.Right,
                                      MaxTotalWidth);

        var sampleLarge = new StoreItemLargeButton();
        _largeMinW = sampleLarge.MinWidth;
        _largePrefW = sampleLarge.PreferredWidth;

        LoadReadyKits();

        foreach (var cat in categories)
        {
            var header = new StoreCategoryButton
            {
                Text = cat.Name,
                Margin = new Thickness(0, 0, 0, 10),
                Id = cat.StoreId,
            };
            header.Click += CategoryHeaderClick;

            StoreStackPanel.Children.Add(header);

            var itemsControl = new ItemsControl
            {
                Margin = new Thickness(0, 0, 0, 35),
                ItemsSource = cat.Items,
                ItemsPanel = (ItemsPanelTemplate)this.Resources["DefaultItemsPanel"]
            };

            _panels.Add((itemsControl, cat));

            StoreStackPanel.Children.Add(itemsControl);
        }

        LargeElementWidth = CalcGrid(MaxTotalWidth);

        CurrentPageWidth = totalWidth;
        // StoreViewBundles.Width = CurrentPageWidth;

        SmallElementWidth = LargeElementWidth * 2;

        SetupElementsWidth();
    }

    private void LoadReadyKits()
    {
        List<ReadyKitModel> kits = StoreHelper.Instance.ReadyKits
            .OrderByDescending(kit => kit.IsRecommended)
            .ToList();

        if (kits.Count == 0)
        {
            StoreViewBundles.ClearBundles();
            return;
        }

        StoreViewBundles.SetBundles(kits.Select(ReadyKitViewModelFactory.Create));
    }

    private async Task UpdateStoreDatabase()
    {
        try
        {
            // The catalogue is parsed again only when the daily sync is due, nothing is loaded yet,
            // or the Store cache was cleaned up (Memory settings delete the catalogue folder).
            TimeSpan sinceSync = DateTime.UtcNow - SettingsManager.Instance.GetValue<DateTime>("STORE", "lastSyncTime");
            bool cacheMissing = !Directory.Exists(CDPIUI.Core.Data.Directories.StoreRepoCacheDirectory);
            bool result = await StoreHelper.Instance.LoadAllStoreDatabase(forseSync: sinceSync.TotalDays >= 1 || cacheMissing);
            // bool result = true;
            if (result)
            {
                List<UICategoryData> categoryDatas = CreateCategoriesList(StoreHelper.Instance.FormattedStoreDatabase);
                InitializeAndShow(categoryDatas);

                ErrorScrollViewer.Visibility = Visibility.Collapsed;
                LoadingGrid.Visibility = Visibility.Collapsed;
                StoreScrollViewer.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorScrollViewer.Visibility = Visibility.Visible;
                LoadingGrid.Visibility = Visibility.Collapsed;
                StoreScrollViewer.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(HomePage), exception.ToString());
        }
    }

    private void StoreHelper_ErrorHappens(string obj)
    {
        ErrorScrollViewer.Visibility = Visibility.Visible;
        ErrorCodeText.Text = obj;
        LoadingGrid.Visibility = Visibility.Collapsed;
        StoreScrollViewer.Visibility = Visibility.Collapsed;
    }
}
