using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Windows.Forms;
using CDPIUI.Shared.Basic.CommandLine;
using CDPIUI.Shared.Migration;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.TrayIcon.ConditionalLaunch;
using CDPIUI.TrayIcon.Forms;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Foundation.Collections;
using Application = System.Windows.Forms.Application;

class Programm
{
    private static Mutex? _singleInstanceMutex;

    private static bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(false, @"Global\CDPIUI_TrayInstance");
        try
        {
            return _singleInstanceMutex.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            // The previous tray ended without releasing it, for example during an update.
            return true;
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains(UninstallHelper.CleanupArgument))
        {
            UninstallHelper.Cleanup();
            return;
        }

        // One tray per machine: the components, the system proxy and the session file in the
        // application folder are shared by all Windows sessions. A second tray would also rename
        // _Update.exe, start the GUI and register hot keys.
        if (!TryAcquireSingleInstance())
        {
            Logger.Instance.CreateWarningLog("Startup", "Another tray is already running; exiting.");
            return;
        }

        InstallExceptionLogging(args);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

        ToastNotificationManagerCompat.History.Clear();

        CleanUpAndStartPipeServer();

        if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
        {
            using var toastContext = new TrayApplicationContext();
            Application.Run(toastContext);
            return;
        }

        StartGuiIfRequested(args);
        FinishUpdate(args);

        if (args.Contains("--autorun"))
        {
            if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide"))
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("TrayHide"), "SHOW_MAIN_WINDOW");
        }

        _ = CheckProgramUpdates();
        _ = BeginCompatibilityCheck();

        using var context = new TrayApplicationContext(args.Contains("--autorun"));
        Application.Run(context);
        ToastNotificationManagerCompat.History.Clear();
    }

    private static void InstallExceptionLogging(string[] args)
    {
        // An exception in a UI event handler is logged instead of ending the tray, which owns
        // the running components and the system proxy.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            Logger.Instance.CreateErrorLog("Tray", $"Unhandled UI exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Instance.CreateDebugLog("Startup",
                $"Unhandled exception; terminating={e.IsTerminating}: {e.ExceptionObject}");
        using (var process = Process.GetCurrentProcess())
            Logger.Instance.CreateDebugLog("Startup",
                $"Starting PID={process.Id}, session={process.SessionId}, path='{Environment.ProcessPath}', " +
                $"autorun={args.Contains("--autorun")}.");
    }

    private static void CleanUpAndStartPipeServer()
    {
        PackageStaging.CleanUp();
        RegeditHelper.RestoreLeftoverSnapshot();

        // Before the pipe starts, so that the GUI never reads a pipe name or a key of an earlier
        // start. The pipe server publishes the new ones after it has created the pipe.
        PipeSessionWriter.RemoveEarlierSession();
        PipeServer.Instance.Init();
        PipeServer.Instance.Start();
    }

    private static void StartGuiIfRequested(string[] args)
    {
        GoodbyeDpiMigrationActivation.TryFindArgument(args, out var migrationRequest);
        if (args.Contains("--show-ui") || args.Contains("--after-patching") ||
            args.Contains("--after-failed-update") || migrationRequest != null)
        {
            // Arguments of a GUI start that had to start the tray first.
            string guiCommandLine = GuiActivationArguments.TryDecode(args, out var guiArgs)
                ? GuiActivationArguments.JoinCommandLine(guiArgs)
                : migrationRequest?.RawArgument ?? string.Empty;
            RunHelper.RunAsDesktopUser(
                Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"),
                guiCommandLine);
        }
    }

    /// <summary>
    /// Replaces Update.exe with the _Update.exe a self-update left, and reports a failed update.
    /// </summary>
    private static void FinishUpdate(string[] args)
    {
        string updateFilePath = Path.Combine(Utils.GetDataDirectory(), "Update.exe");
        string newUpdateFilePath = Path.Combine(Utils.GetDataDirectory(), "_Update.exe");
        try
        {
            if (File.Exists(newUpdateFilePath))
            {
                File.Move(newUpdateFilePath, updateFilePath, overwrite: true);
            }
        }
        catch
        {
            if (args.Contains("--after-patching"))
            {
                Logger.Instance.CreateErrorLog("Update", "Update not finished correctly");
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
            }
        }

        if (args.Contains("--after-failed-update"))
        {
            NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
        }
    }

    public class TrayApplicationContext : ApplicationContext
    {
        private readonly EmptyForm _trayForm;
        private readonly CancellationTokenSource _startupCancellation = new();
        private bool _disposed;

        public TrayApplicationContext(bool runStartupActions = false)
        {
            _trayForm = new EmptyForm();
            // The first form installs the UI context of this thread.
            Utils.UiContext = SynchronizationContext.Current;

            _trayForm.AddIcon(notify: true);
            UninstallHelper.ListenForShutdown(_trayForm, Application.Exit);
            try
            {
                ConditionalLaunchEngine.Instance.Start(_trayForm);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(ConditionalLaunchEngine),
                    $"Conditional launch engine could not be started: {ex}");
            }

            NotifyHelper.Instance.Init();

            if (SettingsManager.Instance.HasUnrecoverableLoadError ||
                File.Exists(SettingsManager.Instance.RecoveryNoticePath))
            {
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("SettingsRecoveryWarning"), "SHOW_MAIN_WINDOW");
            }

            if (runStartupActions)
            {
                // Start after the message loop is available; retries do not depend on the icon.
                _trayForm.BeginInvoke(new Action(() => _ = AskStartupActions(_startupCancellation.Token)));
            }
        }

        protected override void ExitThreadCore()
        {
            if (!_disposed)
                _startupCancellation.Cancel();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _startupCancellation.Cancel();
                _startupCancellation.Dispose();
                _trayForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private static Task AskStartupActions(CancellationToken cancellationToken)
    {
        return StartupActionDispatcher.DispatchAsync(
            () => PipeHelper.SendConPTYPacket(CONPTYMessageIds.GetAllStartupStrings, string.Empty, openIfNotConnected: true),
            message => Logger.Instance.CreateDebugLog("Startup", message),
            cancellationToken);
    }

    private static void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        try
        {
            var _toastArgs = ToastArguments.Parse(toastArgs.Argument);

            if (_toastArgs.TryGetValue("action", out string action))
            {
                _ = NotifyHelper.HandleToastActionFromBackground(action);
            }
            else
            { }

            ValueSet userInput = toastArgs.UserInput;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog("Tray", $"Cannot handle the notification action: {ex}");
        }
    }

    private static async Task CheckProgramUpdates()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(30));
            if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "appUpdates"))
            {
                await PipeHelper.SendCheckUpdatesPacket();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(Programm), $"Update check request failed: {ex}");
        }
    }

    private static async Task BeginCompatibilityCheck()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(60));
            if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "compatibilityCheck"))
            {
                await PipeHelper.SendCompatibilityCheckPacket();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(Programm), $"Compatibility check request failed: {ex}");
        }
    }
}
