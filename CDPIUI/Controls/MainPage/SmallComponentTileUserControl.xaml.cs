using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Helper;
using CDPIUI.ViewModels.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CDPIUI.Controls.MainPage;

public sealed partial class SmallComponentTileUserControl : UserControl
{
    public static readonly DependencyProperty ShowSettingsCommandProperty =
           DependencyProperty.Register(
               nameof(ShowSettingsCommand),
               typeof(ICommand),
               typeof(SmallComponentTileUserControl),
               new PropertyMetadata(null)
           );

    public static readonly DependencyProperty ShowSettingsCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ShowSettingsCommandParameter),
            typeof(object),
            typeof(SmallComponentTileUserControl),
            new PropertyMetadata(null)
        );

    public static readonly DependencyProperty IsOpenedProperty =
        DependencyProperty.Register(
            nameof(IsOpened), typeof(bool), typeof(SmallComponentTileUserControl), new PropertyMetadata(false)
        );

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty CardImageSourceProperty =
        DependencyProperty.Register(
            "UriImageSource", typeof(ImageSource), typeof(SmallComponentTileUserControl), new PropertyMetadata(null)
        );

    public static readonly DependencyProperty CardTitleProperty =
        DependencyProperty.Register(
            "Title", typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty CardDeveloperProperty =
        DependencyProperty.Register(
            "CardDeveloper", typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(
            "CardBackgroundColor", typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata("#000000")
        );

    public SmallComponentTileUserControl()
    {
        ViewModel = new SmallComponentTileViewModel(DispatcherQueue, ShowOutputWindowAsync);

        InitializeComponent();

        HandleTheme();
        ActualThemeChanged += SmallComponentTileUserControl_ActualThemeChanged;
        Unloaded += (_, _) => ViewModel.Detach();
        Loaded += (_, _) => ViewModel.Reload();
    }

    public SmallComponentTileViewModel ViewModel { get; }

    public ICommand ShowSettingsCommand
    {
        get => (ICommand)GetValue(ShowSettingsCommandProperty);
        set => SetValue(ShowSettingsCommandProperty, value);
    }

    public object ShowSettingsCommandParameter
    {
        get => GetValue(ShowSettingsCommandParameterProperty);
        set => SetValue(ShowSettingsCommandParameterProperty, value);
    }

    public bool IsOpened
    {
        get { return (bool)GetValue(IsOpenedProperty); }
        set
        {
            SetValue(IsOpenedProperty, value);
            ViewModel.IsOpened = value;
        }
    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set
        {
            SetValue(StoreIdProperty, value);
            ViewModel.StoreId = value;
            ViewModel.Init();
        }
    }

    public ImageSource CardImageSource
    {
        get { return (ImageSource)GetValue(CardImageSourceProperty); }
        set { SetValue(CardImageSourceProperty, value); }
    }

    public string CardTitle
    {
        get { return (string)GetValue(CardTitleProperty); }
        set { SetValue(CardTitleProperty, value); }
    }

    public string CardDeveloper
    {
        get { return (string)GetValue(CardDeveloperProperty); }
        set { SetValue(CardDeveloperProperty, value); }
    }

    public string CardBackgroundColor
    {
        get { return (string)GetValue(CardBackgroundProperty); }
        set
        {
            SetValue(CardBackgroundProperty, value);
            MainBrush.Color = UIHelper.HexToColorConverter(value);
        }
    }

    private static async Task ShowOutputWindowAsync(string storeId)
    {
        await ((App)Application.Current).UnsafeCreateNewWindow<ViewWindow>(id: storeId);
    }

    private SolidColorBrush StatusBrush(string resourceKey) =>
        (SolidColorBrush)Application.Current.Resources[resourceKey];

    private void SmallComponentTileUserControl_ActualThemeChanged(FrameworkElement sender, object args)
    {
        HandleTheme();
    }

    private void HandleTheme()
    {
        if (ActualTheme == ElementTheme.Dark)
        {
            AcrylicBrush.TintColor = UIHelper.HexToColorConverter("#000000");
        }
        else if (ActualTheme == ElementTheme.Light)
        {
            AcrylicBrush.TintColor = UIHelper.HexToColorConverter("#FFFFFF");
        }
        else
        {
            AcrylicBrush.TintColor = Application.Current.RequestedTheme == ApplicationTheme.Light ? UIHelper.HexToColorConverter("#FFFFFF") : UIHelper.HexToColorConverter("#000000");
        }
    }

    private void ShowMoreSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShowSettingsCommand != null && ShowSettingsCommand.CanExecute(ShowSettingsCommandParameter))
            ShowSettingsCommand.Execute(ShowSettingsCommandParameter);
    }

    private async void ProxyButton_Click(object sender, RoutedEventArgs e)
    {
        ConnectTelegramProxyContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowQueuedAsync();
    }
}
