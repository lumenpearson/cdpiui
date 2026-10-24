using System.Runtime.InteropServices;
using CDPIUI.TrayIcon.ConditionalLaunch;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;
using static CDPIUI.TrayIcon.Forms.TrayIconNativeMethods;

namespace CDPIUI.TrayIcon.Forms;

// Used for creating tray menu handler only.

public partial class EmptyForm : Form
{
    private static readonly Guid IconDisplayGuid = GetGuid();
    private readonly TrayNotifyIcon _notifyIcon;

    private TrayMenuForm? _trayMenuForm;

    public EmptyForm()
    {
        _notifyIcon = new TrayNotifyIcon(this, IconDisplayGuid,
            () => AddIcon(iconName: TrayStatusText.GetCurrentIcon(), toolTip: TrayStatusText.GetNowRunningComponentsString()));

        InitializeComponent();

        HideWindow();

        Application.ApplicationExit += Application_ApplicationExit;
        this.Disposed += EmptyForm_Disposed;

        ConnectHandlers();
    }

    private static Guid GetGuid()
    {
        string savedGUID = SettingsManager.Instance.GetValue<string>("TRAY", "iconGUID");
        if (savedGUID == "NaN" || !Guid.TryParse(savedGUID, out Guid parsedGuid))
        {
            Guid guid = Guid.NewGuid();
            SettingsManager.Instance.SetValue("TRAY", "iconGUID", guid.ToString());
            return guid;
        }
        else
        {
            return parsedGuid;
        }
    }

    private void Application_ApplicationExit(object? sender, EventArgs e)
    {
        Application.ApplicationExit -= Application_ApplicationExit;
        StopComponentsBeforeExit();
        this.Close();
        this.Dispose();
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        StopComponentsBeforeExit();
    }

    private static void StopComponentsBeforeExit()
    {
        try
        {
            // Runs off the UI thread so continuations inside StopAllTasks cannot wait for the blocked message loop.
            Task.Run(() => TasksHelper.Instance.StopAllTasks()).Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(EmptyForm), $"Cannot stop components before exit: {ex.Message}");
        }
    }

    private void ConnectHandlers()
    {
        TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
        TasksHelper.Instance.TaskListUpdated += HandleTasksListUpdate;
        SystemEvents.PowerModeChanged += OnPowerChange;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    private void DisconnectHandlers()
    {
        TasksHelper.Instance.TaskStateUpdated -= HandleTaskStateUpdate;
        TasksHelper.Instance.TaskListUpdated -= HandleTasksListUpdate;
        SystemEvents.PowerModeChanged -= OnPowerChange;
        SystemEvents.SessionEnding -= OnSessionEnding;
    }

    private async void OnPowerChange(object s, PowerModeChangedEventArgs e)
    {
        try
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    await ComponentResumeRestarter.RestartRunningComponentsAsync();
                    break;
                case PowerModes.Suspend:
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(EmptyForm), $"Cannot handle power mode change {e.Mode}: {ex}");
        }
    }

    private void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;
        if (InvokeRequired)
        {
            try
            { BeginInvoke(new Action(() => HandleTaskStateUpdate(taskStateUpdate))); }
            catch (InvalidOperationException)
            {
                // The window handle was destroyed after the check above; the icon is gone too.
            }
            return;
        }
        _notifyIcon.Update(TrayStatusText.GetCurrentIcon(), TrayStatusText.GetNowRunningComponentsString());
    }

    private void HandleTasksListUpdate()
    {
        HandleTaskStateUpdate(Tuple.Create(string.Empty, false));
    }

    private void HideWindow()
    {
        this.ShowInTaskbar = false;
        this.Visible = false;

        StartPosition = FormStartPosition.Manual;

        Location = new Point(-2000, -2000);
        Size = new Size(1, 1);
    }

    private void ShowContextMenuAt(Point location, int vertOffset, int horOffset)
    {
        // A second right click opened another menu and the first one, with its task
        // subscriptions, was never closed.
        HideContextMenu();
        _trayMenuForm = new();
        _trayMenuForm.ShowWindow(location, vertOffset, horOffset);
        _trayMenuForm.Hidden += HideContextMenu;
    }

    private void HideContextMenu()
    {
        if (_trayMenuForm != null)
        {
            _trayMenuForm.Hidden -= HideContextMenu;
            _trayMenuForm.Close();
            _trayMenuForm.Dispose();

            _trayMenuForm = null;
        }
    }

    public void AddIcon(bool notify = false, string? iconName = null, string? toolTip = null)
    {
        if (IsDisposed || Disposing || _notifyIcon.IsAdded)
            return;
        if (string.IsNullOrEmpty(iconName))
            iconName = TrayStatusText.GetCurrentIcon();
        toolTip ??= TrayStatusText.GetNowRunningComponentsString();

        _notifyIcon.Add(notify, iconName, toolTip);
    }

    // Called from WndProc, which must not wait for the pipe. Catches and logs its own errors.
    private static async Task MaximizeApp()
    {
        try
        {
            await PipeHelper.SendOpenWindowPacket("MainWindow", true);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(EmptyForm), $"Cannot open the main window: {ex}");
        }
    }

    private void EmptyForm_Disposed(object? sender, EventArgs e)
    {
        _notifyIcon.Dispose();
        ConditionalLaunchEngine.Instance.Dispose();
        _notifyIcon.Delete();
        DisconnectHandlers();
        this.Disposed -= EmptyForm_Disposed;
    }

    static uint _s_uTaskbarRestart;
    protected override void WndProc(ref Message m)
    {
        if (ConditionalLaunchEngine.Instance.HandleWindowMessage(ref m))
            return;

        if (m.Msg == WM_CREATE)
        {
            _s_uTaskbarRestart = RegisterWindowMessage("TaskbarCreated");
            // Explorer runs at medium integrity; this process requires administrator rights.
            if (_s_uTaskbarRestart == 0 ||
                !ChangeWindowMessageFilterEx(m.HWnd, _s_uTaskbarRestart, 1, IntPtr.Zero))
                Logger.Instance.CreateDebugLog(nameof(EmptyForm),
                    $"Cannot allow TaskbarCreated through UIPI: Win32={Marshal.GetLastWin32Error()}. Timer retry remains available.");
        }
        else if (m.Msg == WM_MYMESSAGE)
        {
            HandleIconMessage(m);
        }
        else
        {
            if (_s_uTaskbarRestart != 0 && m.Msg == _s_uTaskbarRestart)
            {
                Logger.Instance.CreateDebugLog(nameof(EmptyForm), "TaskbarCreated received; restoring tray icon.");
                _notifyIcon.MarkRemoved();
                AddIcon(iconName: TrayStatusText.GetCurrentIcon(), toolTip: TrayStatusText.GetNowRunningComponentsString());
            }
        }

        base.WndProc(ref m);
    }

    private void HandleIconMessage(Message m)
    {
        //(Int32)m.LParam & 0x0000FFFF get the low 2 bytes of LParam, we dont need the high ones.
        //(Int32)m.WParam & 0x0000FFFF is the X coordinate and
        //((Int32)m.WParam & 0xFFFF0000) >> 16 the Y
        switch ((Int32)m.LParam & 0x0000FFFF)
        {
            case NIN_KEYSELECT:
                // Enter or Space on the icon focused with the keyboard.
                _ = MaximizeApp();
                break;
            case WM_CONTEXTMENU:
                var rect = _notifyIcon.GetRect();
                ShowContextMenuAt(new Point(rect.left, rect.top), rect.top - rect.bottom, rect.right - rect.left);
                break;

            //get what mouse messages you want
            case WM_LBUTTONDOWN:
                _ = MaximizeApp();
                break;
            default:
                // NIN_BALLOONHIDE, NIN_BALLOONSHOW, NIN_BALLOONTIMEOUT, NIN_BALLOONUSERCLICK
                // (the user clicked on the balloon), NIN_SELECT and other messages need no action.
                break;
        }
    }
}
