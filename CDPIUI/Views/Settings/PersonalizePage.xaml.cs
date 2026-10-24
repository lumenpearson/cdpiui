using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs;
using CDPIUI.Core;
using CDPIUI.Helper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
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
using Windows.Foundation.Metadata;
using Windows.UI.ViewManagement;
using WinUI3Localizer;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;


namespace CDPIUI.Views.Settings;

public sealed partial class PersonalizePage : TemplatePage
{
    private ObservableCollection<BreadcrumbBarModel> _breadcrumbBarModels = [];

    private ILocalizer _localizer = Localizer.Get();


    private ObservableCollection<GridColumnsCountModel> _gridColumnModels = [];
    private ObservableCollection<ColorViewModel> _colors = [];


    private UISettings _uiSettings = new UISettings();


    public PersonalizePage()
    {
        InitializeComponent();

        IsBackwardAnimationToPageAvailable = true;
        ElementToAnimateBackwardConnectedAnimation = NavGrid;
        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = NavGrid;

        BreadcrumbBar.ItemsSource = _breadcrumbBarModels;
        CreateBreadcrumbBarNavigation();

        ColorsGridView.ItemsSource = _colors;

        CreateColors();
        InitSettings();

        SystemColorBorder.Background = new SolidColorBrush(_uiSettings.GetColorValue(UIColorType.Accent));
        _uiSettings.ColorValuesChanged += HandleAccentChanged;


    }

    private void InitSettings()
    {
        MainGridColumnSelector.ItemsSource = _gridColumnModels;
        CreateGridColimnVariants();
        MainGridColumnSelector.SelectedItem = _gridColumnModels.FirstOrDefault(x => x.Count == SettingsManager.Instance.GetValue<int>("APPEARANCE", "mainGridColumnsCount"));
        MainGridColumnSelector.SelectionChanged += MainGridColumnSelector_SelectionChanged;

        CheckFontSettings();

        string color = SettingsManager.Instance.GetValue<string>("APPEARANCE", "accentColor");
        if (color.StartsWith('#'))
        {
            SystemColorGridView.SelectedItem = null;
            SetColor(color);
        }
        else
        {
            SystemColorGridView.SelectedIndex = 0;
            ColorsGridView.SelectedItem = null;
        }

        PreferSystemWindowTitleBarToggleSwitch.IsOn = SettingsManager.Instance.GetValue<int>("APPEARANCE", "titleBarMode") == 0 ? true : false;

        ShowFlashlightToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "showFlashlightWidget");
        ShowWidgetsToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "showWidgetsPanel");

        InfoStackPanel.Visibility = Visibility.Collapsed;
    }

    private void CreateThemes()
    {

    }

    private void CreateGridColimnVariants()
    {
        _gridColumnModels.Clear();
        _gridColumnModels.Add(new()
        {
            Count = -1,
            DisplayName = _localizer.GetLocalizedString("MainPageGridColumnsCountAuto")
        });
        _gridColumnModels.Add(new()
        {
            Count = 1,
            DisplayName = _localizer.GetLocalizedString("MainPageGridColumnsCountOne")
        });
        _gridColumnModels.Add(new()
        {
            Count = 2,
            DisplayName = _localizer.GetLocalizedString("MainPageGridColumnsCountTwo")
        });
        _gridColumnModels.Add(new()
        {
            Count = 4,
            DisplayName = _localizer.GetLocalizedString("MainPageGridColumnsCountFour")
        });
    }

    private void SetColor(string hex)
    {
        var selColor = _colors.FirstOrDefault(x => x.Hex == hex);
        if (selColor is null)
        {
            _colors.Add(new ColorViewModel(hex));
        }
        ColorsGridView.SelectedIndex = _colors.IndexOf(_colors.FirstOrDefault(x => x.Hex == hex));
    }

    private void CreateColors()
    {
        _colors.Clear();
        _colors.Add(new ColorViewModel("#0078d7"));
        _colors.Add(new ColorViewModel("#00838c"));
        _colors.Add(new ColorViewModel("#e3008c"));
        _colors.Add(new ColorViewModel("#ca4f07"));
        _colors.Add(new ColorViewModel("#e81123"));
        _colors.Add(new ColorViewModel("#00819e"));
        _colors.Add(new ColorViewModel("#10893e"));
        _colors.Add(new ColorViewModel("#881798"));
        _colors.Add(new ColorViewModel("#c239b3"));
        _colors.Add(new ColorViewModel("#767676"));
        _colors.Add(new ColorViewModel("#e1b12c"));
        _colors.Add(new ColorViewModel("#16a085"));
        _colors.Add(new ColorViewModel("#0984e3"));
        _colors.Add(new ColorViewModel("#4a69bd"));
        _colors.Add(new ColorViewModel("#05c46b"));
    }

    private void HandleAccentChanged(UISettings sender, object args)
    {
        var accentColor = sender.GetColorValue(UIColorType.Accent);

        // UISettings raises the event on a background thread; a brush made there throws
        // COMException.
        DispatcherQueue.TryEnqueue(() => SystemColorBorder.Background = new SolidColorBrush(accentColor));
    }

    private void CheckFontSettings()
    {
        string family = ConsoleFontHelper.Instance.FontFamily.Source.ToString();
        string size = ConsoleFontHelper.Instance.FontSize.ToString();

        SelectFontSettingsCard.Description = string.Format(_localizer.GetLocalizedString("FontSizeDescription"), family, size);
    }


    #region Basic

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        MainGridColumnSelector.SelectionChanged -= MainGridColumnSelector_SelectionChanged;
        _uiSettings.ColorValuesChanged -= HandleAccentChanged;

        try
        {
            if (SettingsPage.MainSettingsNavigationSupportedPages.Contains(e.SourcePageType))
            {
                PrepareToConnectedForwardAnimate(NavGrid);
            }
        }
        // The connected animation is cosmetic; the navigation goes on without it.
        catch { }
    }


    public void CreateBreadcrumbBarNavigation()
    {
        _breadcrumbBarModels.Clear();
        _breadcrumbBarModels.Add(new()
        {
            DisplayName = _localizer.GetLocalizedString("Settings"),
            Tag = typeof(SettingsPage)
        });
        _breadcrumbBarModels.Add(new()
        {
            DisplayName = _localizer.GetLocalizedString("Personalization"),
            Tag = this.GetType()
        });
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = (BreadcrumbBarModel)args.Item;
        Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    #endregion


    private void ColorSelectorButton_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:personalization-colors"));
    }

    private void PreferSystemWindowTitleBarToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue<int>("APPEARANCE", "titleBarMode", PreferSystemWindowTitleBarToggleSwitch.IsOn ? 0 : 1);
        InfoStackPanel.Visibility = Visibility.Visible;
    }

    private void MainGridColumnSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SettingsManager.Instance.SetValue<int>("APPEARANCE", "mainGridColumnsCount", ((GridColumnsCountModel)MainGridColumnSelector.SelectedItem).Count);
    }



    private void ColorsGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        /*
        if (ColorsGridView.SelectedItem != null && ColorsGridView.SelectedItem is ColorViewModel color)
        {
            SettingsManager.Instance.SetValue("APPEARANCE", "accentColor", color.Hex);
            ((App)Application.Current).ChangeAccentColor(color.Hex);
            SystemColorGridView.SelectedItem = null;
        }
        */
    }

    private void SystemColorGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        /*
        if (SystemColorGridView.SelectedItem != null)
        {
            SettingsManager.Instance.SetValue("APPEARANCE", "accentColor", "SYSTEM");
            ((App)Application.Current).ChangeAccentColor("SYSTEM");
            ColorsGridView.SelectedItem = null;
        }
        */
    }

    private void ShowFlashlightToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue<bool>("APPEARANCE", "showFlashlightWidget", ShowFlashlightToggleSwitch.IsOn);
    }

    private void ShowWidgetsToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue<bool>("APPEARANCE", "showWidgetsPanel", ShowWidgetsToggleSwitch.IsOn);
    }

    private void MarkupGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        MainGridColumnSelectorSettingsCard.Visibility = ((MainPageMarkupViewModel)MarkupGridView.SelectedItem).Type == MarkupTypes.Modern ? Visibility.Visible : Visibility.Collapsed;
        SettingsManager.Instance.SetValue<string>("APPEARANCE", "mainPageMarkup", ((MainPageMarkupViewModel)MarkupGridView.SelectedItem).Type.ToString());
    }

    private void SelectFontButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ConsoleFontHelper.Instance.ShowFontSettingsDialogForXamlRoot(XamlRoot); // ShowFontSettingsDialogForXamlRoot catches and logs its own errors.
        CheckFontSettings();
    }
}
