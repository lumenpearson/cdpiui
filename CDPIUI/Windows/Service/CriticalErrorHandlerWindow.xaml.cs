using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core;
using CDPIUI.Core.System;
using CDPIUI.Default;
using CDPIUI.Helper;
using CDPIUI.Helper.Basic;
using CDPIUI.Helper.WindowHelper;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class CriticalErrorHandlerWindow : TemplateWindow
{
    private const string FlashlightError = "ERR_FLASHLIGHT_BUSY";

    // Embedded RESW remains available when the GUI localizer itself could not start.
    public string EmergencyText(string key) => Helper.UserExperience.EmergencyLocalization.Get(key);

    public CriticalErrorHandlerWindow(
        string where = null,
        string why = null,
        string errorCode = FlashlightError
        ) : base(useSavedSettings: false)
    {
        InitializeComponent();
        ((App)Application.Current).ShowWindowModalAsync(this);

        WindowTitle = EmergencyText("CriticalErrorWindowTitle");
        EmergencyRecovery.BestEffort(() => IconUri = @"Assets/Icons/Error.ico");

        DisableResizeFeature();


        this.Closed += CriticalErrorHandlerWindow_Closed;

        WhereTextBlock.Text = string.IsNullOrWhiteSpace(where) ? EmergencyText("CriticalErrorUnknown") : where;
        WhyTextBlock.Text = string.IsNullOrWhiteSpace(why) ? EmergencyText("CriticalErrorInfoNotProvided") : why;
        ErrorCodeTextBlock.Text = string.IsNullOrWhiteSpace(errorCode) ? FlashlightError : errorCode;

        string version = EmergencyRecovery.Read(() => ApplicationInfo.Version, EmergencyText("CriticalErrorUnknown"));
        string additionalInfo = EmergencyRecovery.Read(() => string.Format(EmergencyText("CriticalErrorEnvironment"),
            version, Environment.OSVersion, RuntimeInformation.OSArchitecture), EmergencyText("CriticalErrorInfoNotProvided"));
        AdditionalTextBlock.Text = additionalInfo;

        AppWindow.Resize(new Windows.Graphics.SizeInt32(700, 520));
    }

    private void CriticalErrorHandlerWindow_Closed(object sender, WindowEventArgs args)
    {
        this.Closed -= CriticalErrorHandlerWindow_Closed;
        Process.GetCurrentProcess().Kill();
    }


    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Process.GetCurrentProcess().Kill();
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        _ = UrlOpenHelper.LaunchReportUrl();
    }

    private CancellationTokenSource _copyTimerCts;

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        string readyToCopyText =
            $"Critical Exception Info\n" +
            $"```\n" +
            $"Where: {WhereTextBlock.Text}\n" +
            $"Why: {WhyTextBlock.Text}\n" +
            $"ErrCode: {ErrorCodeTextBlock.Text}\n" +
            $"EnvInfo:\n{AdditionalTextBlock.Text}\n" +
            $"```\n" +
            $"Endlog";
        System.Windows.Clipboard.SetText(readyToCopyText);

        _copyTimerCts?.Cancel();
        _copyTimerCts?.Dispose();
        _copyTimerCts = new CancellationTokenSource();
        var token = _copyTimerCts.Token;

        CopyText.Visibility = Visibility.Collapsed;
        CopiedText.Visibility = Visibility.Visible;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);

            CopiedText.Visibility = Visibility.Collapsed;
            CopyText.Visibility = Visibility.Visible;

            _copyTimerCts.Dispose();
            _copyTimerCts = null;
        }
        catch (TaskCanceledException)
        {
            // A new copy restarted the timer; that copy restores the text.
        }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        ShellHelper.LookupDirectory(Path.Combine(CDPIUI.Core.Data.Directories.DataDirectory, "Logs"));
    }
}
