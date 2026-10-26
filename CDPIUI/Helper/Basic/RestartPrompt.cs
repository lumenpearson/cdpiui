using System;
using System.Diagnostics;
using System.IO;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI;

/// <summary>
/// A driver or runtime package can ask for a system restart. The main window, classic or modern,
/// shows the request; before, only the classic window did, and the request was only logged.
/// </summary>
internal static class RestartPrompt
{
    public static void Attach(Window window)
    {
        bool dialogShown = false;

        void OnRestartRequired(string itemId)
        {
            window.DispatcherQueue?.TryEnqueue(async () =>
            {
                if (dialogShown || window.Content?.XamlRoot == null || !CoreEvents.Instance.IsRestartPending)
                    return;
                dialogShown = true;
                CoreEvents.Instance.AcknowledgeRestart();
                var localizer = WinUI3Localizer.Localizer.Get();
                ContentDialog dialog = new()
                {
                    XamlRoot = window.Content.XamlRoot,
                    Title = localizer.GetLocalizedString("RestartRequiredTitle"),
                    Content = localizer.GetLocalizedString("RestartRequiredMessage"),
                    PrimaryButtonText = localizer.GetLocalizedString("RestartNow"),
                    CloseButtonText = localizer.GetLocalizedString("RestartLater"),
                    DefaultButton = ContentDialogButton.Close,
                };
                try
                {
                    if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
                    {
                        string shutdown = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
                        Process.Start(new ProcessStartInfo(shutdown, "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(nameof(RestartPrompt), $"Restart request failed: {ex.Message}");
                }
                finally
                {
                    dialogShown = false;
                }
            });
        }

        // CoreEvents outlives the window; a reopened window must not add a second handler.
        CoreEvents.Instance.RestartRequired += OnRestartRequired;
        window.Closed += (_, _) => CoreEvents.Instance.RestartRequired -= OnRestartRequired;

        // A request raised while no main window was open waits for the next one.
        if (window.Content is FrameworkElement root)
        {
            root.Loaded += (_, _) =>
            {
                if (CoreEvents.Instance.IsRestartPending)
                    OnRestartRequired(string.Empty);
            };
        }
    }
}
