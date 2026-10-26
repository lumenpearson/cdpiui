using CDPIUI.Controls.Default;
using CDPIUI.ViewModels.BlockCheck2;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TextDecorations = Windows.UI.Text.TextDecorations;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage : TemplatePage, IBlockCheck2MainPageView
{
    public BlockCheck2MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = new BlockCheck2MainPageViewModel(this);
        InitializeComponent();
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = UtilityButtons;
    }

    public void CancelRunningSession() => ViewModel.Session.Cancel();

    public static InfoBarSeverity ToInfoBarSeverity(BlockCheck2NoticeSeverity severity) => severity switch
    {
        BlockCheck2NoticeSeverity.Success => InfoBarSeverity.Success,
        BlockCheck2NoticeSeverity.Warning => InfoBarSeverity.Warning,
        BlockCheck2NoticeSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };

    public void GoToStep(BlockCheck2WizardStep step) => MainContent.GoTo(step switch
    {
        BlockCheck2WizardStep.Mode => ModeStep,
        BlockCheck2WizardStep.Advanced => AdvancedStep,
        BlockCheck2WizardStep.Review => ReviewStep,
        BlockCheck2WizardStep.Progress => ProgressStep,
        BlockCheck2WizardStep.WorkComplete => WorkComplete,
        _ => SiteListsStep,
    });

    public void ShowNavigationButtons(BlockCheck2NavigationButtons buttons) =>
        UtilityButtons.SetButtonVisibilities(
            (ExitButton, buttons.Exit ? Visibility.Visible : Visibility.Collapsed),
            (BackButton, buttons.Back ? Visibility.Visible : Visibility.Collapsed),
            (CancelButton, buttons.Cancel ? Visibility.Visible : Visibility.Collapsed),
            (ViewResultButton, buttons.ViewResult ? Visibility.Visible : Visibility.Collapsed),
            (NextButton, buttons.Next ? Visibility.Visible : Visibility.Collapsed));

    private async void MainPage_Loaded(object sender, RoutedEventArgs e) => await ViewModel.OnLoadedAsync();

    private void MainPage_Unloaded(object sender, RoutedEventArgs e) => ViewModel.OnUnloaded();

    private async void NextButton_Click(object sender, RoutedEventArgs e) => await ViewModel.NextAsync();

    private void ResetAdvancedButton_Click(object sender, RoutedEventArgs e) => ViewModel.ResetAdvancedOptions();

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.GoBack())
        {
            return;
        }

        if (Frame.CanGoBack)
        {
            PrepareToConnectedBackwardAnimate(UtilityButtons);
            Frame.GoBack();
        }
        else
        {
            CreateConfigUtilWindow.Instance.Close();
        }
    }

    private async void ViewResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Session.LastReport == null)
        {
            return;
        }

        BlockCheck2ResultWindow window = await ((App)Application.Current)
            .SafeCreateNewWindow<BlockCheck2ResultWindow>();
        if (ViewModel.Session.LastSession != null)
        {
            window.ShowSession(ViewModel.Session.LastSession);
        }
        else
        {
            window.ShowReport(ViewModel.Session.LastReport);
        }

        CreateConfigUtilWindow.Instance?.Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigUtilWindow.Instance.Close();
    }

    private void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RadioButton radioButton)
        {
            radioButton.IsChecked = true;
        }
    }

    private void ProgressViewMoreButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleAdditionalProgressInfo();
        ProgressViewMoreText.TextDecorations = TextDecorations.Underline;
    }

    private void ProgressViewMoreButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        ProgressViewMoreText.TextDecorations = TextDecorations.None;

    private void ProgressViewMoreButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        ProgressViewMoreText.TextDecorations = TextDecorations.Underline;
}
