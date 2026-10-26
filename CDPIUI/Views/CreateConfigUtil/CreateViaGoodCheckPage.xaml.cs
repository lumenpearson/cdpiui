using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Basic.Settings;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Extensions;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Unidecode.NET;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.CreateConfigUtil;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class CreateViaGoodCheck : TemplatePage
{
    private readonly DispatcherQueue _uiDispatcher;

    private enum States
    {
        None,
        LetsBegin,
        PrepareLoading,
        SiteListsSettings,
        StrategyListSettings,
        AdditionalSettings,
    }

    private States _currentState = States.None;

    private List<GoodCheckStrategiesList> _strategiesLists = [];
    private List<GoodCheckSitelistButton> _sitelistButtons = [];

    private List<OneSiteListOneStrategyModeElement> _oneSiteListOneStrategyModeElements = [];

    private ObservableCollection<ComboBoxModel> _resolverItems = [];

    private IniSettingsHelper IniSettingsHelper { get; set; }

    private const string AddOnId = "ASGKOI001";

    private string ComponentId = string.Empty;

    private ILocalizer _localizer = Localizer.Get();

    public CreateViaGoodCheck()
    {
        InitializeComponent();
        ResolverComboBox.ItemsSource = _resolverItems;
        InitResolverComboBox();
        _uiDispatcher = DispatcherQueue.GetForCurrentThread();
    }

    private void InitResolverComboBox()
    {
        _resolverItems.Clear();
        _resolverItems.Add(new ComboBoxModel() { DisplayName = _localizer.GetLocalizedString("/GoodCheckAdditionalSettings/NativeResolver"), Id = "native" });
        _resolverItems.Add(new ComboBoxModel() { DisplayName = _localizer.GetLocalizedString("/GoodCheckAdditionalSettings/Curl"), Id = "curl" });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            ComponentId = Parameter.Get("componentId");
        }

        if (!string.IsNullOrEmpty(ComponentId))
        {
            if (ComponentId == HardcodedItemIds.ComponentIds[Core.Store.Data.Components.GoodbyeDPI])
            {
                ChangeModeComboBox.IsEnabled = false;
            }

            HideAll();
            SetLoadingMode(false);
            SwitchState(States.LetsBegin);
        }
    }

    private void HideAll()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        LetsBeginStackPanel.Visibility = Visibility.Collapsed;
        GoodCheckSettingsStackPanel.Visibility = Visibility.Collapsed;
    }

    private void SetLoadingMode(
        bool isLoading,
        bool isIndeterminate = true,
        bool cancellationAvailable = false,
        string headerText = ""
        )
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => SetLoadingMode(isLoading, isIndeterminate, cancellationAvailable, headerText));
            return;
        }

        if (isLoading)
        {
            GoBackButton.Visibility = Visibility.Collapsed;
            GoForwardButton.Visibility = Visibility.Collapsed;

            LoadingProgressBar.IsIndeterminate = isIndeterminate;
            LoadingHeader.Text = headerText;
            CancelButton.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = cancellationAvailable;
            LoadingStateGrid.Visibility = Visibility.Visible;
        }
        else
        {
            GoBackButton.Visibility = Visibility.Visible;
            GoForwardButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Collapsed;

            LoadingStateGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_currentState)
        {
            case States.SiteListsSettings:
                GoForwardButton.IsEnabled = true;
                SwitchState(States.LetsBegin, slideToLeft: false);
                break;
            case States.LetsBegin:
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else
                {
                    CreateConfigUtilWindow.Instance.Close();
                }

                break;
            default:
                SwitchState(_currentState - 1, slideToLeft: false);
                break;
        }
    }

    private async void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_currentState)
        {
            case States.LetsBegin:
                SwitchState(States.PrepareLoading);
                await InitGoodCheckSiteListsPage();
                break;
            case States.SiteListsSettings:
                SwitchState(States.StrategyListSettings);
                InitGoodCheckStrategiesListsPage();
                if (StrategiesListCombobox.SelectedIndex != -1 || ChangeModeComboBox.IsChecked == true)
                {
                    GoForwardButton.IsEnabled = true;
                }
                else
                {
                    GoForwardButton.IsEnabled = false;
                }
                break;
            case States.StrategyListSettings:
                SwitchState(States.AdditionalSettings);
                LoadGoodCheckSettings();
                break;
            case States.AdditionalSettings:
                SaveGoodCheckSettings();
                Frame.Navigate(typeof(GoodCheckWorkPage));
                StartGoodCheck();
                break;
            default:
                SwitchState(_currentState + 1);
                break;
        }
        await Task.CompletedTask;
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private UIElement GetPanelForState(States s)
    {
        return s switch
        {
            States.PrepareLoading => LoadingStackPanel,
            States.LetsBegin => LetsBeginStackPanel,
            States.SiteListsSettings => GoodCheckSettingsStackPanel,
            States.StrategyListSettings => ChooseStrategiesListStackPanel,
            States.AdditionalSettings => AdditionalSettingsStackPanel,
            _ => null
        };
    }

    private void SwitchState(States newState, bool slideToLeft = true)
    {
        if (newState == _currentState)
            return;

        var oldPanel = GetPanelForState(_currentState);
        var newPanel = GetPanelForState(newState);
        if (newPanel == null)
            return;

        double width = ContentGrid.ActualWidth;
        if (width <= 0)
            width = DisplayArea.Primary.WorkArea.Width;

        PanelSlideTransition.Run(oldPanel, newPanel, width, slideToLeft);

        _currentState = newState;
    }

    private void SelectedSitelistScrollViewer_LayoutUpdated(object sender, object e)
    {
        SelectedSitelistScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - GoodCheckSettingsTextContent.ActualHeight;
        SelectedSitelistScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - GoodCheckSettingsTextContent.ActualHeight;
    }

    private void SelectedSitelistStackPanel_LayoutUpdated(object sender, object e)
    {
        if (_currentState == States.SiteListsSettings)
        {
            if (SelectedSitelistStackPanel.Children.Count > 0)
            {
                GoForwardButton.IsEnabled = true;
                SelectedSitelistTip.Visibility = Visibility.Collapsed;
            }
            else
            {
                GoForwardButton.IsEnabled = false;
                SelectedSitelistTip.Visibility = Visibility.Visible;
            }
        }
    }

    private void FileIncorrectWarning_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void FileIncorrectWarning_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        args.Cancel = true;

        EventHandler<object> onDone = null;
        onDone = (s, ev) =>
        {
            CloseStoryboard.Completed -= onDone;
            InfoBarTranslate.X = 200;
            sender.Opacity = 0;
        };

        CloseStoryboard.Completed += onDone;
        CloseStoryboard.Begin();
    }

    private void CloseStoryboard_Completed(object sender, object e)
    {
        FileIncorrectWarning.Visibility = Visibility.Collapsed;
    }

    private void StrategiesListCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StrategiesListCombobox.SelectedIndex != -1)
        {
            GoForwardButton.IsEnabled = true;
        }
        else
        {
            GoForwardButton.IsEnabled = false;
        }
    }

    private void ChangeModeComboBox_Click(object sender, RoutedEventArgs e)
    {
        if (ChangeModeComboBox.IsChecked == true)
        {
            OneListOneStrategyStackPanel.Visibility = Visibility.Visible;
            AllListsOneStrategyPanel.Visibility = Visibility.Collapsed;


        }
        else
        {
            OneListOneStrategyStackPanel.Visibility = Visibility.Collapsed;
            AllListsOneStrategyPanel.Visibility = Visibility.Visible;
        }

        if (StrategiesListCombobox.SelectedIndex != -1 || ChangeModeComboBox.IsChecked == true)
        {
            GoForwardButton.IsEnabled = true;
        }
        else
        {
            GoForwardButton.IsEnabled = false;
        }
    }

    private void OneListOneStrategyScrollViewer_LayoutUpdated(object sender, object e)
    {
        OneListOneStrategyScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - ChooseStrategiesListTextContent.ActualHeight;
        OneListOneStrategyScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - ChooseStrategiesListTextContent.ActualHeight;
    }

    private void AdditionalSettingsScrollViewer_LayoutUpdated(object sender, object e)
    {
        AdditionalSettingsScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - AdditionalSettingsListTextContent.ActualHeight;
        AdditionalSettingsScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - AdditionalSettingsListTextContent.ActualHeight;
    }

    private void ResolverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private async Task ShowErrorDialog(string message = null)
    {
        try
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = _localizer.GetLocalizedString("ContinuationImpossible"),
                Content = string.IsNullOrEmpty(message) ?
                    string.Format(_localizer.GetLocalizedString("AddOnNotInstalled"),
                    HardcodedItemIds.AddOnsIds.GetKeyByValue(AddOnId), AddOnId) : message,
                PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText"),
                XamlRoot = this.XamlRoot,
            };
            await dialog.ShowQueuedAsync();
            CreateConfigUtilWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
            window.Close();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(CreateViaGoodCheck), exception.ToString());
        }
    }
}
