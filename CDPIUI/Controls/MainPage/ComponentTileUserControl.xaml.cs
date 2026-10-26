using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Helper;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ComponentViews;
using CDPIUI.Views.CreateConfigUtil;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CDPIUI.Controls.MainPage;

public sealed partial class ComponentTileUserControl : UserControl, IComponentTileView
{
    public ComponentTileUserControl()
    {
        ViewModel = new ComponentTileViewModel(DispatcherQueue, this);

        InitializeComponent();

        CheckVisualState();

        SizeChanged += ComponentTileUserControl_SizeChanged;
        Unloaded += ComponentTileUserControl_Unloaded;
    }

    public ComponentTileViewModel ViewModel { get; }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set
        {
            if (string.Equals(value, GetValue(StoreIdProperty)))
                return;
            Debug.WriteLine($"Init for {value}, {GetValue(StoreIdProperty)}");
            SetValue(StoreIdProperty, value);

            ViewModel.StoreId = value;
            _ = ViewModel.InitAsync(); // InitAsync catches and logs its own errors.
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

    public async Task ShowOutputWindowAsync(string storeId)
    {
        await ((App)Application.Current).UnsafeCreateNewWindow<ViewWindow>(id: storeId);
    }

    public async Task ShowComponentSettingsAsync(string storeId)
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();
        window.NavView_Navigate(typeof(ViewComponentSettingsPage),
            new NameValueCollection()
            {
                { "componentId", storeId }
            },
            new DrillInNavigationTransitionInfo());
    }

    private SolidColorBrush StatusBrush(string resourceKey) =>
        (SolidColorBrush)Application.Current.Resources[resourceKey];

    private Brush AccentBrush(string hexColor) =>
        hexColor == null ? null : UIHelper.HexToSolidColorBrushConverter(hexColor);

    private void ComponentTileUserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CheckVisualState();
    }

    private void ComponentTileUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= ComponentTileUserControl_SizeChanged;
        ViewModel.Detach();
    }

    private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SelectConfigAsync(ConfigChooseCombobox.SelectedItem as ConfigSelectorItem);
    }

    private async void ConfigSettingToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        var toggleSwitch = (ToggleSwitch)sender;
        await ViewModel.SetConfigSettingAsync((string)toggleSwitch.Tag, toggleSwitch.IsOn);
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
}
