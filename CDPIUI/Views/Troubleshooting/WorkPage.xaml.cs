using CDPIUI.Controls.Default;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels.Troubleshooting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;

namespace CDPIUI.Views.Troubleshooting;

/// <summary>
/// Runs a troubleshooting check and its fix. <see cref="WorkViewModel"/> holds the state;
/// the page keeps the navigation, the connected animation and the window actions.
/// </summary>
public sealed partial class WorkPage : TemplatePage, IWorkView
{
    public WorkViewModel ViewModel { get; }

    public WorkPage()
    {
        ViewModel = new WorkViewModel(this);
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ActionButtonsGrid;

        ViewModel.ConnectEvents();
        Loaded += (_, _) => ViewModel.ConnectEvents();
        Unloaded += (_, _) => ViewModel.DisconnectEvents();
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
            ViewModel.Start(Parameter.Get("action").ToEnum<NavigationParameters>(NavigationParameters.None));
    }

    void IWorkView.MakeWindowModal()
    {
        var app = (App)Application.Current;
        app.ShowWindowModalAsync(app.GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    void IWorkView.MakeWindowNormal()
    {
        var app = (App)Application.Current;
        app.MakeWindowNormal(app.GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        (await ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>()).Close();
    }
}
