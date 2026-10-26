using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CDPIUI.AddOns.Troubleshooting;
using CDPIUI.Controls.Default;
using CDPIUI.Helper.LScript;
using CDPIUI.Shared.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using static System.Windows.Forms.AxHost;
using static CDPIUI.AddOns.Troubleshooting.TroubleshootingService;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Troubleshooting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public enum NavigationParameters
{
    None,
    BeginBasicCheck,
    BeginStoreRepoCheck,
}

public class DiagnosticResultModel
{
    public bool IsHighlighted { get; set; }
    public string CriterionName { get; set; }
    public string CurrentValue { get; set; }
    public string TargetValue { get; set; }
    public string IconGlyph { get; set; }
    public bool IsHelpAvailable { get; set; }
    public bool IsFixAvailable { get; set; }
    public string HelpText { get; set; }
    public string HelpUrl { get; set; }
}

public sealed partial class WorkPage : TemplatePage
{
    private ILocalizer _localizer = Localizer.Get();

    private ObservableCollection<DiagnosticResultModel> _displayDiagnosticResultsCollection = [];
    private ObservableCollection<DiagnosticResultModel> _fixResults = [];
    private NavigationParameters _navigationParameter = NavigationParameters.None;
    public WorkPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ActionButtonsGrid;

        DiagnosticResultListView.ItemsSource = _displayDiagnosticResultsCollection;
        FixResultListView.ItemsSource = _fixResults;

        ConnectEvents();
        Loaded += (_, _) => ConnectEvents();
        Unloaded += (_, _) => DisconnectEvents();
    }

    // The services are singletons: a page that is not shown must not keep receiving their events.
    private bool _eventsConnected;

    private void ConnectEvents()
    {
        if (_eventsConnected)
            return;
        _eventsConnected = true;
        TroubleshootingService.Instance.CurrentStateChanged += TroubleshootingHelper_BasicDialogStateChanged;
    }

    private void DisconnectEvents()
    {
        if (!_eventsConnected)
            return;
        _eventsConnected = false;
        TroubleshootingService.Instance.CurrentStateChanged -= TroubleshootingHelper_BasicDialogStateChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        try
        {
            PrepareToConnectedBackwardAnimate(ActionButtonsGrid);
        }
        // The connected animation is cosmetic; the navigation goes on without it.
        catch { }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            NavigationParameters navp = Parameter.Get("action").ToEnum<NavigationParameters>(NavigationParameters.None);

            _navigationParameter = navp;
            switch (_navigationParameter)
            {
                // The checks catch and log their own errors.
                case NavigationParameters.BeginBasicCheck:
                    _ = BeginBasicCheck();
                    break;
                case NavigationParameters.BeginStoreRepoCheck:
                    _ = BeginStoreRepoCheck();
                    break;
            }
        }

    }

    private void TroubleshootingHelper_BasicDialogStateChanged(Enum state)
    {
        if (state.ToString() == "Preparing" || state.ToString() == "Completed")
        {
            CurrentStateTextBlock.Text = _localizer.GetLocalizedString($"/Troubleshooting/{state}");
        }
        else
        {
            CurrentStateTextBlock.Text = string.Format(_localizer.GetLocalizedString("/Troubleshooting/WorkingOn"), _localizer.GetLocalizedString($"/Troubleshooting/{state}"));
        }
    }

    private string GetHelpFor(string key)
    {
        string result = _localizer.GetLocalizedString($"/Flashlight/{key}");

        return LScriptLangHelper.ExecuteScript(result, scriptArgs: "/Flashlight/");
    }

    private void GetReady()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Collapsed;
        DiagnosticResultStackPanel.Visibility = Visibility.Collapsed;
        FixStackPanel.Visibility = Visibility.Collapsed;
        FixResultStackPanel.Visibility = Visibility.Collapsed;

        _displayDiagnosticResultsCollection.Clear();
        _fixResults.Clear();

        ForwardButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible;

        ((App)Application.Current).ShowWindowModalAsync(((App)Application.Current).GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private void GetReadyForCheck()
    {
        GetReady();
        LoadingStackPanel.Visibility = Visibility.Visible;
    }

    private void GetReadyForFix()
    {
        GetReady();
        FixStackPanel.Visibility = Visibility.Visible;
    }

    private void CheckViewResultActions()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Visible;
        DiagnosticResultStackPanel.Visibility = Visibility.Visible;
    }

    private void CheckCompleteActions()
    {
        ForwardButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        ((App)Application.Current).MakeWindowNormal(((App)Application.Current).GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private void FixViewResultActions()
    {
        FixStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Visible;
        FixResultStackPanel.Visibility = Visibility.Visible;
    }

    private void FixCompleteActions()
    {
        ExitButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        ((App)Application.Current).MakeWindowNormal(((App)Application.Current).GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private object _diagnosticResults = null;

    private async Task BeginBasicCheck()
    {
        try
        {
            GetReadyForCheck();

            _diagnosticResults = await TroubleshootingService.Instance.RunBasicDiagnostic();

            CheckViewResultActions();

            DisplayDiagnosticResults<RunBasicDialogStates>((Dictionary<RunBasicDialogStates, bool>)_diagnosticResults, TroubleshootingService.BasicDiagnosticStateModelPairs);

            CheckCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(WorkPage), exception.ToString());
        }
    }

    private async Task BeginBasicFix()
    {
        try
        {
            GetReadyForFix();

            var result = await TroubleshootingService.Instance.FixAllBasicErrors((Dictionary<RunBasicDialogStates, bool>)_diagnosticResults);

            FixViewResultActions();

            DisplayFixResult<RunBasicDialogStates>(result, BasicDiagnosticStateModelPairs);

            FixCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(WorkPage), exception.ToString());
        }
    }

    private async Task BeginStoreRepoCheck()
    {
        try
        {
            GetReadyForCheck();

            _diagnosticResults = await TroubleshootingService.Instance.RunStoreDiagnostic();

            CheckViewResultActions();

            DisplayDiagnosticResults<StoreCheckStates>((Dictionary<StoreCheckStates, bool>)_diagnosticResults, TroubleshootingService.StoreDiagnosticStateModelPairs);

            CheckCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(WorkPage), exception.ToString());
        }
    }

    private async Task BeginStoreFix()
    {
        try
        {
            GetReadyForFix();

            var result = await TroubleshootingService.Instance.FixAllStoreErrors((Dictionary<StoreCheckStates, bool>)_diagnosticResults);

            FixViewResultActions();

            DisplayFixResult<StoreCheckStates>(result, StoreDiagnosticStateModelPairs);

            FixCompleteActions();
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(WorkPage), exception.ToString());
        }
    }

    private void DisplayDiagnosticResults<T>(Dictionary<T, bool> diagnosticResult, Dictionary<T, DiagnosticStateModel> diagnosticStatesPair) where T : Enum
    {
        bool isErrorHappens = false;
        foreach (var criterion in diagnosticResult)
        {
            bool isCorrect = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.CorrectState == criterion.Value;
            string correctText = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            _displayDiagnosticResultsCollection.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = _localizer.GetLocalizedString($"/Troubleshooting/{criterion.Key}"),
                CurrentValue = isCorrect ? _localizer.GetLocalizedString($"/Troubleshooting/{correctText}") : _localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = _localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "\uE930" : "\uEA39",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = GetHelpFor($"{criterion.Key}"),
                HelpUrl = GetUrl(criterion.Key.ToString())
            });

            if (!isErrorHappens)
                isErrorHappens = !isCorrect;
        }

        if (isErrorHappens)
        {
            ResultTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultErrorTitle");
            ResultTipTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultErrorTip");
        }
        else
        {
            ResultTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultTitle");
            ResultTipTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultTip");

        }
    }

    private void DisplayFixResult<T>(Dictionary<T, FixResultModel> fixResult, Dictionary<T, DiagnosticStateModel> diagnosticStatesPair) where T : Enum
    {
        bool isErrorHappens = false;

        foreach (var crit in fixResult)
        {
            bool isCorrect = crit.Value.IsFixed;
            if (isCorrect)
                continue;

            string correctText = diagnosticStatesPair.GetValueOrDefault(crit.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = diagnosticStatesPair.GetValueOrDefault(crit.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            _fixResults.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = _localizer.GetLocalizedString($"/Troubleshooting/{crit.Key}"),
                CurrentValue = _localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = _localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "\uE930" : "\uEA39",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = GetHelpFor($"{crit.Key}"),
                HelpUrl = GetUrl(crit.Key.ToString())
            });

            if (!isErrorHappens)
                isErrorHappens = !isCorrect;
        }


        if (isErrorHappens)
        {
            ResultTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/FixResultErrorTitle");
            ResultTipTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/FixResultErrorTip");
        }
        else
        {
            ResultTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/FixResultTitle");
            ResultTipTextBlock.Text = _localizer.GetLocalizedString("/Troubleshooting/FixResultTip");
            FixResultListView.Visibility = Visibility.Collapsed;
        }
    }

    private string GetUrl(string key)
    {

        string url = _localizer.GetLocalizedString($"/Flashlight/URL_{key}");
        if (string.IsNullOrEmpty(url))
        {
            string name = _localizer.GetLocalizedString($"/Troubleshooting/{key}");
            url = string.Format(_localizer.GetLocalizedString("/Flashlight/NetSearchPlaceholder"), name);
        }
        if (url == "$EMPTY")
            return string.Empty;
        return url;
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        Commands.CommandsHandler.HandleCommand(
            "cdpiui://Help/Utils/TroubleshootingUtility/");
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        // The fixes catch and log their own errors.
        if (_navigationParameter == NavigationParameters.BeginBasicCheck)
        {
            _ = BeginBasicFix();
        }
        else if (_navigationParameter == NavigationParameters.BeginStoreRepoCheck)
        {
            _ = BeginStoreFix();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        (await ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>()).Close();
    }
}
