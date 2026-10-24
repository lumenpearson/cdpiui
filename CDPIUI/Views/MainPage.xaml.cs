using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.MainPage;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using CDPIUI.ViewModels;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation.Metadata;
using WinUI3Localizer;


namespace CDPIUI.Views;


public sealed partial class MainPage : TemplatePage
{
    private ObservableCollection<ViewStoreItemModel> _components = [];

    private ILocalizer _localizer = Localizer.Get();

    public ICommand ShowComponentSettingsClickCommand { get; }

    private FrameworkElement _storedElement;

    public MainPage()
    {
        this.InitializeComponent();
        this.DataContext = this;
        this.NavigationCacheMode = NavigationCacheMode.Enabled;

        MainListView.ItemsSource = _components;

        LoadComponents();
        ShowComponentSettingsClickCommand = new RelayCommand(p => ShowComponentSettings(p));

        ConnectEvents();
        Loaded += (_, _) => ConnectEvents();
        Unloaded += (_, _) => DisconnectEvents();

        IsBackwardAnimationToPageAvailable = true;
    }

    // The services are singletons: a page that is not shown must not keep receiving their events.
    private bool _eventsConnected;

    private void ConnectEvents()
    {
        if (_eventsConnected)
            return;
        _eventsConnected = true;
        StoreHelper.Instance.ItemActionsStopped += Instance_ItemActionsStopped;
        StoreHelper.Instance.ItemRemoved += Instance_ItemRemoved;
    }

    private void DisconnectEvents()
    {
        if (!_eventsConnected)
            return;
        _eventsConnected = false;
        StoreHelper.Instance.ItemActionsStopped -= Instance_ItemActionsStopped;
        StoreHelper.Instance.ItemRemoved -= Instance_ItemRemoved;
    }

    private void Instance_ItemRemoved(string obj)
    {
        LoadComponents();
    }

    private void Instance_ItemActionsStopped(string obj)
    {
        LoadComponents();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private void LoadComponents()
    {
        _components.Clear();
        List<DatabaseStoreItem> installedComponents = DatabaseHelper.Instance.GetItemsByType("component");

        foreach (DatabaseStoreItem installedComponent in installedComponents)
        {
            _components.Add(new ViewStoreItemModel()
            {
                StoreId = installedComponent.Id,
                ImageSource = new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(installedComponent.IconPath))),
                Name = string.IsNullOrEmpty(installedComponent.ShortName) ? installedComponent.Id : installedComponent.ShortName,
                ColorHEX = installedComponent.BackgroundColor
            });
        }

        if (_components.Count > 0)
        {
            ComponentTilePlaceholder.Visibility = Visibility.Collapsed;
            FlashlightContainer.Visibility = Visibility.Visible;
        }
        else
        {
            ComponentTilePlaceholder.Visibility = Visibility.Visible;
            FlashlightContainer.Visibility = Visibility.Collapsed;
        }


    }

    private void TryAnimate()
    {
        _storedElement = null;
    }

    private void ShowComponentSettings(object p)
    {
        var item = MainListView.ContainerFromItem((ViewStoreItemModel)p);
        if (item is FrameworkElement fw)
        {
            _storedElement = fw;
            ElementToAnimateBackwardConnectedAnimation = _storedElement;
            PrepareToConnectedForwardAnimate(fw);
        }
        var nvc = new NameValueCollection
        {
            { "componentId", ((ViewStoreItemModel)p).StoreId }
        };
        Frame.Navigate(typeof(ViewComponentSettingsPage), nvc, new DrillInNavigationTransitionInfo());
    }

    private void MainListView_Loaded(object sender, RoutedEventArgs e)
    {
        TryAnimate();
        FlashlightTipsWidgetUserControl.ConnectHandlers();
    }


}
