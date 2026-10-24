using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Shared;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigUtil;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using ToolTip = Microsoft.UI.Xaml.Controls.ToolTip;
using ToolTipService = Microsoft.UI.Xaml.Controls.ToolTipService;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.MainPage;

public sealed partial class ComponentTileUserControl : UserControl
{
    private ILocalizer _localizer = Localizer.Get();

    public ICommand ViewOutputClickCommand { get; }
    public ICommand ViewSettingsButtonClickCommand { get; }
    public ICommand ToggleProcessButtonClickCommand { get; }

    private ObservableCollection<ConfigSelectorItem> _configItems = new();
    private ObservableCollection<ConfigSettingsItem> _configSettingsList = [];
    private ObservableCollection<FeaturesViewModel> _availableFeaturesList = [];
    private int _configLoadVersion;






    public ComponentTileUserControl()
    {
        InitializeComponent();

        ViewOutputIconButton.DisplayText = _localizer.GetLocalizedString("ViewOutputIconButton");
        SettingsIconButton.DisplayText = _localizer.GetLocalizedString("SettingsIconButton");

        // The command methods catch and log their own errors.
        ViewOutputClickCommand = new RelayCommand(p => _ = ViewOutputButtonClick());
        ViewSettingsButtonClickCommand = new RelayCommand(p => _ = ViewSettingsButtonClick());
        ToggleProcessButtonClickCommand = new RelayCommand(p => _ = ToggleProcessButtonClick());

        ComponentTasksManager.Instance.TaskStateUpdated += TaskUpdated;

        CheckVisualState();

        this.SizeChanged += ComponentTileUserControl_SizeChanged;

        ConfigChooseCombobox.ItemsSource = _configItems;
        _configItems.CollectionChanged += ConfigItems_CollectionChanged;
        ConfigSettingsListView.ItemsSource = _configSettingsList;
        AdditionalFeaturesListView.ItemsSource = _availableFeaturesList;

        StatusFontIcon.Glyph = SharedUtils.IsOsSupportedNewGlyph() ? "\uF4A5" : "\uE8B0";

        this.Unloaded += ComponentTileUserControl_Unloaded;
    }

    private void ComponentTileUserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CheckVisualState();
    }

    private void ComponentTileUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        this.SizeChanged -= ComponentTileUserControl_SizeChanged;
        this.Loaded -= ComponentTileUserControl_Unloaded;
        _configItems.CollectionChanged -= ConfigItems_CollectionChanged;
        ComponentTasksManager.Instance.TaskStateUpdated -= TaskUpdated;

        ComponentHelper componentHelper =
            ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                StoreId);
        if (componentHelper is null)
            return;

        componentHelper.ConfigListUpdated -= LoadConfigItems;
    }

    public void CheckVisualState()
    {
        if (ActualWidth < 400)
        {
            VisualStateManager.GoToState(this, "SmallVisualState", true);
        }
        else if (ActualWidth < 824)
        {
            VisualStateManager.GoToState(this, "MediumVisualState", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "BigVisualState", true);
        }
    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set
        {
            if (string.Equals(value, GetValue(StoreIdProperty)))
                return;
            Debug.WriteLine($"Init for {value}, {GetValue(StoreIdProperty)}");
            SetValue(StoreIdProperty, value);

            _ = Init(); // Init catches and logs its own errors.
        }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(ComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public string ImageUrl
    {
        get { return (string)GetValue(ImageUrlProperty); }
        set
        {
            SetValue(ImageUrlProperty, value);
            MainImage.Source = new BitmapImage(new Uri(value));
        }
    }

    public static readonly DependencyProperty ImageUrlProperty =
        DependencyProperty.Register(
            nameof(ImageUrl), typeof(string), typeof(ComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    private void TaskUpdated(Tuple<string, bool> tuple)
    {
        // Can be raised on a thread pool thread (BlockCheck2 and preset tests stop and start
        // tasks there). Reading StoreId off the UI thread threw and the handlers after this
        // one did not get the event.
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (tuple.Item1 != StoreId)
                return;
            _ = PreferTaskStateActions(); // PreferTaskStateActions catches and logs its own errors.
        });
    }

    private async Task Init()
    {
        try
        {
            TitleTextBlock.Text = DatabaseHelper.Instance.GetItemById(StoreId)?.ShortName.NullIfEmpty() ?? StoreId;

            if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
            {
                // PreferTaskStateActions and CheckComponentState catch and log their own errors.
                _ = PreferTaskStateActions();

                TileBackgroungBorder.Background = UIHelper.HexToSolidColorBrushConverter(DatabaseHelper.Instance.GetItemById(StoreId).BackgroundColor);

                bool isAddToAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", StoreId], "usedForAutorun");
                AutorunBadgeGrid.Visibility = isAddToAutorun ? Visibility.Visible : Visibility.Collapsed;
                _ = CheckComponentState();

                string st = StoreId;

                GetAvailableFeaturesForItem();

                await Task.Run(() => LoadConfigInfo(st));
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }

    private void GetAvailableFeaturesForItem()
    {
        _availableFeaturesList.Clear();
        FeaturesData.AvailableFeaturesForComponent.TryGetValue(StoreId, out var availableFeatures);
        if (availableFeatures == null)
            return;

        foreach (var feature in availableFeatures)
        {
            FeaturesData.AvailableComponentFeatureImages.TryGetValue(feature, out var imageSource);
            if (imageSource == null)
                continue;

            _availableFeaturesList.Add(new()
            {
                DisplayName = _localizer.GetLocalizedString(feature.ToString()),
                Id = feature,
                Image = new Uri(imageSource)
            });
        }
    }

    private async void AvailableFeaturesButton_Click(object sender, RoutedEventArgs e)
    {
        var action = (AvailableComponentFeatures)((Button)sender).Tag;
        switch (action)
        {
            case AvailableComponentFeatures.SetupProxy:
                _ = ((App)Application.Current).SafeCreateNewWindow<ProxySetupUtilWindow>();
                break;
            case AvailableComponentFeatures.AutoSelectConfig:
                CreateConfigUtilWindow gwindow = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
                gwindow.TargetStoreId = StoreId;
                break;
            case AvailableComponentFeatures.CreateConfig:
                await ConfigMakerWindow.CreateForComponentAsync(StoreId);
                break;
            case AvailableComponentFeatures.ExploreNewConfigs:
                StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
                window.NavigateSubPage(typeof(Views.Store.CategoryViewPage),
                    new NameValueCollection() {
                        { "categoryId", "C002CS" }
                    },
                    new SuppressNavigationTransitionInfo());
                break;
            case AvailableComponentFeatures.VisitForum:
                await UrlOpenHelper.LaunchComponentForumUrl(StoreId);
                break;
            case AvailableComponentFeatures.ConnectTgWsProxy:
                ConnectTelegramProxyContentDialog dialog = new()
                {
                    XamlRoot = this.XamlRoot,
                };
                await dialog.ShowQueuedAsync();
                break;
        }
    }

    private void GoToComponentSettings_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewSettingsButtonClick(); // ViewSettingsButtonClick catches and logs its own errors.
    }
}
