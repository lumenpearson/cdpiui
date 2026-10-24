using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.ViewModels.Store;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using WinUI3Localizer;

namespace CDPIUI.Controls.Store;

public sealed partial class StoreDownloadItemButton : UserControl
{
    public static readonly DependencyProperty ClickCommandProperty =
        DependencyProperty.Register(
            nameof(ClickCommand),
            typeof(ICommand),
            typeof(StoreDownloadItemButton),
            new PropertyMetadata(null)
        );

    public ICommand ClickCommand
    {
        get => (ICommand)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public static readonly DependencyProperty ClickCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ClickCommandParameter),
            typeof(object),
            typeof(StoreDownloadItemButton),
            new PropertyMetadata(null)
        );

    public object ClickCommandParameter
    {
        get => GetValue(ClickCommandParameterProperty);
        set => SetValue(ClickCommandParameterProperty, value);
    }

    private MarkdownConfig _config;

    public MarkdownConfig MarkdownConfig
    {
        get => _config;
        set => _config = value;
    }

    private ILocalizer _localizer = Localizer.Get();

    public StoreDownloadItemButtonViewModel ViewModel { get; }

    public StoreDownloadItemButton()
    {
        ViewModel = new StoreDownloadItemButtonViewModel(ExecuteClickCommand);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        InitializeComponent();
        _config = new MarkdownConfig();

        this.Loaded += StoreDownloadItemButton_Loaded;
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(StoreDownloadItemButtonViewModel.ActionGlyph):
                ActionGlyph = ViewModel.ActionGlyph;
                break;
            case nameof(StoreDownloadItemButtonViewModel.Tooltip):
                Tooltip = ViewModel.Tooltip;
                break;
            case nameof(StoreDownloadItemButtonViewModel.IsProgressCritical):
                // The brushes come from the application resources, so the view sets them.
                ProgressBar.Foreground = ViewModel.IsProgressCritical
                    ? new SolidColorBrush((Color)Application.Current.Resources["SystemFillColorCritical"])
                    : (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                break;
        }
    }

    private void StoreDownloadItemButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (DescriptionText.ActualHeight > 150)
        {
            ViewMoreButton.Visibility = Visibility.Visible;
        }
        DescriptionText.MaxHeight = 150;
    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set
        {
            SetValue(StoreIdProperty, value);
            ViewModel.OnStoreIdSet(IsErrorHappens);
        }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty, OnStoreIdChanged)
        );

    private static void OnStoreIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Also runs when the value is set without the CLR setter.
        ((StoreDownloadItemButton)d).ViewModel.StoreId = (string)e.NewValue;
    }

    public string OperationId
    {
        get { return (string)GetValue(OperationIdProperty); }
        set { SetValue(OperationIdProperty, value); }
    }

    public static readonly DependencyProperty OperationIdProperty =
        DependencyProperty.Register(
            nameof(OperationId), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );

    public string Developer
    {
        get { return (string)GetValue(DeveloperProperty); }
        set { SetValue(DeveloperProperty, value); }
    }

    public static readonly DependencyProperty DeveloperProperty =
        DependencyProperty.Register(
            nameof(Developer), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );

    public string Tooltip
    {
        get { return (string)GetValue(TooltipProperty); }
        set { SetValue(TooltipProperty, value); }
    }

    public static readonly DependencyProperty TooltipProperty =
        DependencyProperty.Register(
            nameof(Tooltip), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(Localizer.Get().GetLocalizedString("Cancel"))
        );

    public string Category
    {
        get { return (string)GetValue(CategoryProperty); }
        set { SetValue(CategoryProperty, value); }
    }

    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(
            nameof(Category), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );

    public string CurrentVersion
    {
        get { return (string)GetValue(CurrentVersionProperty); }
        set { SetValue(CurrentVersionProperty, value); }
    }

    public static readonly DependencyProperty CurrentVersionProperty =
        DependencyProperty.Register(
            nameof(CurrentVersion), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );

    public string ServerVersion
    {
        get { return (string)GetValue(ServerVersionProperty); }
        set { SetValue(ServerVersionProperty, value); }
    }

    public static readonly DependencyProperty ServerVersionProperty =
        DependencyProperty.Register(
            nameof(ServerVersion), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set
        {
            if (string.IsNullOrEmpty(value))
                SetValue(DescriptionProperty, _localizer.GetLocalizedString("DeveloperNotCommentedRelease"));
            else
                SetValue(DescriptionProperty, value);
        }
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(string.Empty)
        );
    public bool IsUpdateLikeButton
    {
        get { return (bool)GetValue(IsUpdateLikeButtonProperty); }
        set
        {
            SetValue(IsUpdateLikeButtonProperty, value);
            ViewModel.SetUpdateLike(value, OperationId);
        }
    }

    public static readonly DependencyProperty IsUpdateLikeButtonProperty =
        DependencyProperty.Register(
            nameof(IsUpdateLikeButton), typeof(bool), typeof(StoreDownloadItemButton), new PropertyMetadata(false)
        );

    public string ActionGlyph
    {
        get { return (string)GetValue(ActionGlyphProperty); }
        set { SetValue(ActionGlyphProperty, value); }
    }

    public static readonly DependencyProperty ActionGlyphProperty =
        DependencyProperty.Register(
            nameof(ActionGlyph), typeof(string), typeof(StoreDownloadItemButton), new PropertyMetadata(StoreDownloadItemButtonViewModel.CancelGlyph)
        );
    public bool IsErrorHappens
    {
        get { return (bool)GetValue(IsErrorHappensProperty); }
        set
        {
            SetValue(IsErrorHappensProperty, value);
            ViewModel.SetErrorHappens(value);
        }
    }

    public static readonly DependencyProperty IsErrorHappensProperty =
        DependencyProperty.Register(
            nameof(IsErrorHappens), typeof(bool), typeof(StoreDownloadItemButton), new PropertyMetadata(false)
        );

    public ImageSource CardImageSource
    {
        get { return (ImageSource)GetValue(CardImageSourceProperty); }
        set { SetValue(CardImageSourceProperty, value); }
    }

    public static readonly DependencyProperty CardImageSourceProperty =
        DependencyProperty.Register(
            nameof(CardImageSource), typeof(ImageSource), typeof(StoreDownloadItemButton), new PropertyMetadata(null)
        );

    public Brush CardBackgroundBrush
    {
        get { return (Brush)GetValue(CardBackgroundProperty); }
        set
        {
            SetValue(CardBackgroundProperty, value);
            Rectangle.Fill = value;
        }
    }

    public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(
            nameof(CardBackgroundBrush),
            typeof(Brush),
            typeof(StoreDownloadItemButton),
            new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
        );

    private void ExecuteClickCommand()
    {
        if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
            ClickCommand.Execute(ClickCommandParameter);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        ConnectedAnimationService.GetForCurrentView()
            .PrepareToAnimate("ForwardConnectedAnimation", PART_Image);

        StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), new NameValueCollection() { { "itemId", StoreId } }, new SuppressNavigationTransitionInfo());
    }

    private async void ViewMoreButton_Click(object sender, RoutedEventArgs e)
    {
        MarkdownTextViewContentDialog dialog = new MarkdownTextViewContentDialog()
        {
            XamlRoot = this.XamlRoot,
            Title = _localizer.GetLocalizedString("ViewMore"),
            Text = Description
        };

        await dialog.ShowQueuedAsync();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteClickCommand();
    }
}
