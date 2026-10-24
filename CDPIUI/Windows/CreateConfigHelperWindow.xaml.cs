using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Default;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI;

public sealed partial class CreateConfigHelperWindow : TemplateWindow
{
    public static CreateConfigHelperWindow Instanse { get; private set; }
    public bool IsOperationExitAskAvailable { get; set; } = false;

    private bool _isDialogRequested = false;
    private ConfigItem _configItemToEditRequsted = null;

    private ILocalizer _localizer = Localizer.Get();

    public CreateConfigHelperWindow()
    {
        this.InitializeComponent();

        WindowTitle = _localizer.GetLocalizedString("CreateConfigHelperWindowTitle");
        IconUri = @"Assets/Icons/Edit.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        WindowsPositionHelper.TrySetMicaBackdrop(true, this, MainGrid);

        MainFrame = ContentFrame;


        Instanse = this;

        ContentFrame.Navigate(typeof(Views.CreateConfigHelper.MainPage), new NameValueCollection(), new DrillInNavigationTransitionInfo());
        this.Closed += CreateConfigHelperWindow_Closed;

        if (this.Content is FrameworkElement fe)
        {
            fe.Loaded += Fe_Loaded;
        }

        SetEditorBackgroundSettings();

        SetStatus();
    }

    public void OpenConfigEditPage(bool skp = false, ConfigItem configItem = null)
    {
        _isDialogRequested = true;
        _configItemToEditRequsted = configItem;
        if (skp)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                if (configItem == null)
                {
                    SelectConfigToEditContentDialog dialog = new SelectConfigToEditContentDialog()
                    {
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowQueuedAsync();
                    if (dialog.SelectedConfigResult == SelectResult.Selected)
                    {
                        configItem = dialog.SelectedConfigItem;
                        ContentFrame.Navigate(
                            typeof(CreateNewConfigPage),
                            CreateConfigNavigationParameters.EditConfig(configItem),
                            new DrillInNavigationTransitionInfo());
                    }
                }
            });
        }
    }

    public void CreateNewConfigForComponentId(string componentId)
    {
        ContentFrame.Navigate(
            typeof(CreateNewConfigPage),
            CreateConfigNavigationParameters.CreateForComponent(componentId),
            new DrillInNavigationTransitionInfo());
    }

    public void CreateNewConfigFromString(string componentId, string startupString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(startupString);

        ContentFrame.Navigate(
            typeof(CreateNewConfigPage),
            CreateConfigNavigationParameters.CreateFromString(componentId, startupString),
            new DrillInNavigationTransitionInfo());
    }

    public void OpenGoodCheckReportFromFile(string filePath)
    {
        ContentFrame.Navigate(typeof(ViewGoodCheckReportPage),
            CreateConfigNavigationParameters.GoodCheckReport(filePath),
            new DrillInNavigationTransitionInfo());
    }

    public void EditConfigKit(string kitId)
    {
        ContentFrame.Navigate(
            typeof(EditConfigKitPage),
            CreateConfigNavigationParameters.ConfigKit(kitId),
            new DrillInNavigationTransitionInfo());
    }

    public void SetStatus(bool isWorking = false, string text = "")
    {
        ContentFrame.DispatcherQueue.TryEnqueue(() =>
        {
            StatusProgressIcon.Visibility = isWorking ? Visibility.Collapsed : Visibility.Visible;
            StatusProgressRing.Visibility = isWorking ? Visibility.Visible : Visibility.Collapsed;
            StatusTextBlock.Text = string.IsNullOrEmpty(text) ? _localizer.GetLocalizedString("Ready") : text;
        });
    }

    private void Fe_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isDialogRequested)
        {
            OpenConfigEditPage(true, configItem: _configItemToEditRequsted);
        }

        BackButton.Visibility = Visibility.Collapsed;

        if (this.Content is FrameworkElement fe)
        {
            fe.Loaded -= Fe_Loaded;
        }
    }

    private void CreateConfigHelperWindow_Closed(object sender, WindowEventArgs args)
    {
        Instanse = null;
    }

    private async Task AskForExit(NavigatingCancelEventArgs e)
    {
        try
        {
            ContentDialog exitDialog = new ContentDialog()
            {
                Title = _localizer.GetLocalizedString("Exit"),
                Content = _localizer.GetLocalizedString("ExitAsk"),
                PrimaryButtonText = _localizer.GetLocalizedString("Yes"),
                CloseButtonText = _localizer.GetLocalizedString("No"),
                XamlRoot = this.Content.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };
            var result = await exitDialog.ShowQueuedAsync();
            if (result == ContentDialogResult.Primary)
            {
                ContentFrame.DispatcherQueue.TryEnqueue(() =>
                {
                    IsOperationExitAskAvailable = false;
                    if (ContentFrame.CanGoBack)
                    {
                        NavigateBackWithParameter(e.Parameter);
                        return;
                    }
                    ContentFrame.Navigate(e.SourcePageType, e.Parameter, new DrillInNavigationTransitionInfo());

                });
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(CreateConfigHelperWindow), exception.ToString());
        }
    }

    private void AuditMenuItemsEnabled(Type pageType)
    {
        HomeItem.IsEnabled = true;
        CreateNewConfigButton.IsEnabled = true;
        EditMenuItem.Visibility = Visibility.Collapsed;
        ViewMenuItem.Visibility = Visibility.Collapsed;

        ContentFrame.Style = (Style)MainGrid.Resources["ContentFrameDefaultStyle"];

        if (pageType == typeof(Views.CreateConfigHelper.MainPage))
        {
            HomeItem.IsEnabled = false;
        }
        else if (pageType == typeof(CreateNewConfigPage))
        {
            CreateNewConfigButton.IsEnabled = false;
            EditMenuItem.Visibility = Visibility.Visible;
            ViewMenuItem.Visibility = Visibility.Visible;
            ContentFrame.Style = (Style)MainGrid.Resources["ContentFrameTransparentStyle"];
        }
    }

    private void ContentFrame_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        if (IsOperationExitAskAvailable)
        {
            // AskForExit catches and logs its own errors.
            _ = AskForExit(e);
            e.Cancel = true;
            return;

        }

        if (e.Cancel != true)
        {
            AuditMenuItemsEnabled(e.SourcePageType);
            if (e.SourcePageType == typeof(Views.CreateConfigHelper.MainPage))
            {
                ContentFrame.DispatcherQueue.TryEnqueue(() =>
                {
                    if (ContentFrame.CanGoBack)
                        ContentFrame.BackStack.Clear();
                });
            }

        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        BackButton.Visibility = (ContentFrame.CanGoBack && ContentFrame.SourcePageType != typeof(Views.CreateConfigHelper.MainPage)) ? Visibility.Visible : Visibility.Collapsed;

        UpdateCurrentHelpItem(e.SourcePageType);

        ContentFrame.ForwardStack.Clear();
    }

    private void UpdateCurrentHelpItem(Type page)
    {
        CurrentHelpMenuFlyoutItem.Text = string.Format(_localizer.GetLocalizedString("/Flashlight/GetHelpFor"), _localizer.GetLocalizedString($"/Flashlight/{page.Name}"));
    }

    private void HomeItem_Click(object sender, RoutedEventArgs e)
    {
        bool result = RemoveAndGoBackTo(typeof(Views.CreateConfigHelper.MainPage), ContentFrame);
        if (!result)
        {
            ContentFrame.Navigate(typeof(Views.CreateConfigHelper.MainPage), new NameValueCollection(), new DrillInNavigationTransitionInfo());
        }
    }

    private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(this.SearchAnimatedIcon, "PointerOver");
    }

    private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(this.SearchAnimatedIcon, "Normal");
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }
}
