using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CDPIUI.Shared.ComponentsTask;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.TrayIcon.Controls;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.TrayIcon.Forms;

public partial class TrayMenuForm : Form
{
    private readonly int _defaultWidth;

    public Action? Hidden;

    public TrayMenuForm()
    {
        _defaultWidth = 285;

        HideWindow();
        this.ShowInTaskbar = false;
        InitializeComponent();

        StartPosition = FormStartPosition.Manual;
        MaximizeButton.DisplayImage = Utils.GetBitmapFromResourses("CDPIUI.TrayIcon.Assets.trayLogoNormal.ico");
        AllowTransparency = false;
        this.Visible = false;
        this.KeyPreview = true;

        FormBorderStyle = FormBorderStyle.None;

        this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        this.SetStyle(ControlStyles.UserPaint, true);
        this.SetStyle(ControlStyles.ResizeRedraw, true);
        this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

        ConnectHandlers();

        CheckWindowHeight();

        ExitButton.DisplayText = LocaleHelper.GetLocaleString("Exit");
        MaximizeButton.DisplayText = LocaleHelper.GetLocaleString("ShowMainWindow");

        this.Load += TrayMenuForm_Load;

    }

    private void TrayMenuForm_Load(object? sender, EventArgs e)
    {
        this.Load -= TrayMenuForm_Load;
        HandleTasksListUpdate();
    }

    #region HandlersManage
    private void ConnectHandlers()
    {
        TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
        TasksHelper.Instance.TaskListUpdated += HandleTasksListUpdate;
        TasksHelper.Instance.TaskSetupStateUpdated += HandleTaskSetupStateUpdate;

        MaximizeButton.Clicked += MaximizeButton_MouseClick;
        ExitButton.Clicked += ExitButton_MouseClick;

        this.Disposed += TrayMenuForm_Disposed;
        this.KeyDown += TrayMenuForm_KeyDown;
    }



    private void DisconnectHandlers()
    {
        TasksHelper.Instance.TaskStateUpdated -= HandleTaskStateUpdate;
        TasksHelper.Instance.TaskListUpdated -= HandleTasksListUpdate;
        TasksHelper.Instance.TaskSetupStateUpdated -= HandleTaskSetupStateUpdate;

        MaximizeButton.Clicked -= MaximizeButton_MouseClick;
        ExitButton.Clicked -= ExitButton_MouseClick;

        DisconnectComponentPanelChildrenHandlers();

        this.Disposed -= TrayMenuForm_Disposed;
        this.KeyDown -= TrayMenuForm_KeyDown;
    }

    private void DisconnectComponentPanelChildrenHandlers()
    {
        foreach (Control control in ComponentPanel.Controls.Cast<Control>().ToList())
        {
            if (control is ComponentUserControl componentUserControl)
            {
                componentUserControl.EventHappens -= HandleEventHappens;
                componentUserControl.Dispose();
            }
            else if (control is ModernButtonUserControl buttonUserControl)
            {
                buttonUserControl.Clicked -= HandleButtonClick;
                buttonUserControl.Dispose();
            }
        }
    }

    #endregion

    private void TrayMenuForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            HideWindow();
        }
    }

    private void ExitButton_MouseClick(object? sender, EventArgs e)
    {
        HandleEventHappens();
        _ = PipeHelper.SendApplicationPacket(ApplicationMessageIds.CloseApplicationUI);
        this.Dispose();
        Application.Exit();
    }

    private async void MaximizeButton_MouseClick(object? sender, EventArgs e)
    {
        try
        {
            HandleEventHappens();

            await PipeHelper.SendOpenWindowPacket("MainWindow", true);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(TrayMenuForm), $"Cannot open the main window: {ex}");
        }
    }

    private void HandleEventHappens()
    {
        HideWindow();
    }



    private void HandleTasksListUpdate()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        List<TaskModel<ProcessManager>> tasks = TasksHelper.Instance.GetTasksSnapshotSync().DistinctBy(task => task.Id).ToList();

        SafeInvoke(() =>
        {
            ComponentPanel.SuspendLayout();
            DisconnectComponentPanelChildrenHandlers();
            ComponentPanel.Controls.Clear();

            int targetWidth = ComponentPanel.Width;
            int offset = 0;

            if (tasks.Count == 0)
            {
                var storeButton = new ModernButtonUserControl()
                {
                    Name = "OpenStoreButton",
                    DisplayText = LocaleHelper.GetLocaleString("Store"),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Width = targetWidth - ComponentPanel.Padding.Right,
                    Margin = new(0, 2, 4, 2),
                };
                storeButton.Clicked += HandleButtonClick;
                ComponentPanel.Controls.Add(storeButton);
                storeButton.Invalidate();
            }
            else
            {
                foreach (var task in tasks)
                {
                    var control = new ComponentUserControl()
                    {
                        Name = task.Id,
                        DisplayText = LocaleHelper.GetLocalizedComponentName(task.Id),
                        ComponentId = task.Id,
                        IsRunning = task.ProcessManager.IsProcessRunning,
                        IsSetupComplete = task.IsSetupComplete ?? true,
                        Width = targetWidth,
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        Margin = new(0, 2, 0, 2),
                        Top = offset
                    };

                    control.EventHappens += HandleEventHappens;

                    ComponentPanel.Controls.Add(control);
                    control.Invalidate();
                    offset += control.Height + control.Margin.Top + control.Margin.Bottom;
                }
            }

            ComponentPanel.ResumeLayout();
            CheckWindowHeight();
            ComponentPanel.Invalidate();
        });
    }

    // Task events come from other threads; the form can be destroyed between the
    // IsHandleCreated check and Invoke, and the exception ended the tray.
    private void SafeInvoke(Action action)
    {
        try
        {
            if (!IsDisposed && IsHandleCreated)
                Invoke(action);
        }
        catch (ObjectDisposedException)
        {
            // The form was closed after the check above; there is nothing to update.
        }
        catch (InvalidOperationException)
        {
            // The window handle was destroyed after the check above; there is nothing to update.
        }
    }

    private void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate) =>
        UpdateComponentControl(taskStateUpdate.Item1, control => control.IsRunning = taskStateUpdate.Item2);

    private void HandleTaskSetupStateUpdate(Tuple<string, bool> taskStateUpdate) =>
        UpdateComponentControl(taskStateUpdate.Item1, control => control.IsSetupComplete = taskStateUpdate.Item2);

    private void UpdateComponentControl(string id, Action<ComponentUserControl> apply)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        SafeInvoke(() =>
        {
            Control[] controls = ComponentPanel.Controls.Find(id, false);
            if (controls.Length > 0)
            {
                if (controls[0] is ComponentUserControl componentUserControl)
                {
                    apply(componentUserControl);
                }
            }
            else
            {
                HandleTasksListUpdate();
            }
        });
    }

    private async void HandleButtonClick(object? sender, EventArgs eventArgs)
    {
        try
        {
            if (sender is ModernButtonUserControl button)
            {
                switch (button.Name)
                {
                    case "OpenStoreButton":
                        HandleEventHappens();
                        await PipeHelper.SendOpenWindowPacket("StoreWindow", true);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(TrayMenuForm), $"Cannot handle the menu button: {ex}");
        }
    }

    #region AuditHeight

    private void SetBasicActionsPanelHeight()
    {
        BasicActionsPanel.Height = ExitButton.Height + ExitButton.Margin.Bottom + ExitButton.Margin.Top;
        BasicActionsPanel.Height += MaximizeButton.Height + MaximizeButton.Margin.Bottom + MaximizeButton.Margin.Top;
    }

    private int GetComponentsHeight()
    {
        int total = 0;
        foreach (Control control in ComponentPanel.Controls)
        {
            total += control.Height + control.Margin.Top + control.Margin.Bottom;
        }
        ComponentPanel.Height = total;
        Debug.WriteLine(total);
        return total;
    }

    private int CheckWindowHeight()
    {
        SetBasicActionsPanelHeight();
        int requestedHeight = BasicActionsPanel.Height + this.Padding.Bottom + this.Padding.Top +
            Separator.Height + Separator.Margin.Top + Separator.Margin.Bottom + GetComponentsHeight();
        this.Height = requestedHeight;
        MainPanel.Height = requestedHeight - this.Padding.Bottom - this.Padding.Top;
        return requestedHeight;
    }

    #endregion

    #region PublicMethods
    public void HideWindow()
    {
        this.Visible = false;
        Location = new Point(-2000, -2000);
        Size = new Size(1, 1);

        Hidden?.Invoke();
    }
    public void ShowWindow(Point location, int vertOffset, int horOffset)
    {
        this.Visible = true;
        Size = new Size(_defaultWidth, CheckWindowHeight());

        var taskbarPos = Taskbar.DisplayBounds;
        float scaleFactor = GetScalingFactor();

        Location = new Point(x: location.X, y: location.Y);

        var screen = GetScreen();
        var relLoc = Location;

        if (Taskbar.Position == TaskbarPosition.Left)
        {
            relLoc.X = (int)(taskbarPos.Right / scaleFactor);
        }
        else if (Taskbar.Position == TaskbarPosition.Right)
        {
            relLoc.X = (int)(taskbarPos.Left / scaleFactor) - this.Width;
        }
        else if (Taskbar.Position == TaskbarPosition.Top)
        {
            relLoc.Y = (int)(taskbarPos.Bottom / scaleFactor);
        }
        else if (Taskbar.Position == TaskbarPosition.Bottom)
        {
            relLoc.Y = (int)(taskbarPos.Top / scaleFactor) - this.Height;
        }

        if (relLoc.X < screen.Left)
        {
            relLoc.X = screen.Left + horOffset;
        }
        else if (relLoc.X + this.Width > screen.Right)
        {
            relLoc.X = screen.Right - this.Width - horOffset;
        }
        if (relLoc.Y < screen.Top)
        {
            relLoc.Y = screen.Top + this.Height + vertOffset;
        }
        else if (relLoc.Y + this.Height > screen.Bottom)
        {
            relLoc.Y = screen.Bottom - this.Height - vertOffset;
        }

        Location = relLoc;

        this.TopMost = true;
        this.BringToFront();
        this.Activate();
        this.Focus();
        this.Focus();
        this.Select();
    }
    #endregion

    private Rectangle GetScreen()
    {
        return Screen.PrimaryScreen.Bounds;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ACTIVATEAPP)
        {
            if (m.WParam.ToInt64() == 0) /* Being deactivated */
            {
                Debug.WriteLine("Hide");
                HideWindow();
            }
        }
        base.WndProc(ref m);
    }

    private void TrayMenuForm_Disposed(object? sender, EventArgs e)
    {
        DisconnectHandlers();
    }

    #region WinAPI
    const int WM_ACTIVATEAPP = 0x001C;

    // https://stackoverflow.com/a/21450169/32214029
    [DllImport("gdi32.dll")]
    static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
    public enum DeviceCap
    {
        VERTRES = 10,
        DESKTOPVERTRES = 117,

        // http://pinvoke.net/default.aspx/gdi32/GetDeviceCaps.html
    }

    private float GetScalingFactor()
    {
        using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
        IntPtr desktop = g.GetHdc();
        int LogicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES);
        int PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);

        g.ReleaseHdc(desktop);

        float ScreenScalingFactor = (float)PhysicalScreenHeight / (float)LogicalScreenHeight;

        return ScreenScalingFactor;
    }
    #endregion
}
