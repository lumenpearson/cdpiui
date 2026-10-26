using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using Microsoft.UI.Xaml;
using WinUI3Localizer;

namespace CDPIUI;

public partial class App
{
    private int _isUnhandledErrorShown;

    private void RegisterUnhandledExceptionHandlers()
    {
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Instance.CreateErrorLog(nameof(App), $"Fatal unhandled exception: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Instance.CreateErrorLog(nameof(App), $"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Instance.CreateErrorLog(nameof(App), $"Unhandled exception: {e.Exception?.ToString() ?? e.Message}");

        // Exceptions from async void event handlers end up here. Keep the application
        // running and show the error instead of closing every window without a trace.
        e.Handled = true;
        // ShowUnhandledError catches and logs its own errors.
        _ = ShowUnhandledError(e.Exception);
    }

    private async Task ShowUnhandledError(Exception exception)
    {
        if (Interlocked.Exchange(ref _isUnhandledErrorShown, 1) != 0)
            return;

        try
        {
            XamlRoot xamlRoot = OpenWindows.LastOrDefault(w => w.Content?.XamlRoot != null)?.Content.XamlRoot;
            if (xamlRoot == null)
                return;

            string code = exception != null
                ? ErrorsHelper.Convertor.GetPrettyErrorCode("APP", exception)
                : "APP_UNKNOWN";
            await new Controls.Dialogs.Universal.ErrorContentDialog().ShowErrorDialogAsync(
                Localizer.Get().GetLocalizedString("SomethingWentWrong"), code, xamlRoot);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(nameof(App), $"Cannot show unhandled exception: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isUnhandledErrorShown, 0);
        }
    }
}
