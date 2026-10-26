using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;
using Windows.ApplicationModel.Chat;

namespace CDPIUI.TrayIcon.Controls;

public partial class ComponentUserControl : UserControl
{
    private string _text = string.Empty;
    public string DisplayText
    {
        get
        {
            return _text;
        }
        set
        {
            _text = value;
            MainTextBox.Text = value;
        }
    }

    private string _componentId = string.Empty;
    public string ComponentId
    {
        get => _componentId;
        set => _componentId = value;
    }

    private bool _isRunning = false;
    public bool IsRunning
    {
        get { return _isRunning; }
        set
        {
            _isRunning = value;
            StateChangedActions();
        }
    }

    private bool _isSetupComplete = false;
    public bool IsSetupComplete
    {
        get { return _isSetupComplete; }
        set
        {
            _isSetupComplete = value;
            SetupStateChanged();
        }
    }

    public Action? EventHappens;

    public ComponentUserControl()
    {
        InitializeComponent();
        ConnectHandlers();
        StateChangedActions();

        this.Load += ComponentUserControl_Load;
        this.Disposed += ComponentUserControl_Disposed;
    }

    #region HandlersManagement
    private void ConnectHandlers()
    {
        ComponentWorkButton.Clicked += ComponentWorkButton_Clicked;
        OpenPseudoConsoleButton.Clicked += OpenPseudoConsoleButton_Clicked;
        OpenSettingsButton.Clicked += OpenSettingsButton_Clicked;


    }

    private void DisconnectHandlers()
    {
        ComponentWorkButton.Clicked -= ComponentWorkButton_Clicked;
        OpenPseudoConsoleButton.Clicked -= OpenPseudoConsoleButton_Clicked;
        OpenSettingsButton.Clicked -= OpenSettingsButton_Clicked;
    }
    #endregion

    private void ComponentUserControl_Load(object? sender, EventArgs e)
    {
        InitToolTips();
        this.Load -= ComponentUserControl_Load;
    }

    private void InitToolTips()
    {
        ComponentWorkButton.ToolTip = BasicToolTip;
        OpenPseudoConsoleButton.ToolTip = BasicToolTip;
        OpenSettingsButton.ToolTip = BasicToolTip;

        ComponentWorkButton.ToolTipMessage = LocaleHelper.GetLocaleString("ToggleState");
        OpenPseudoConsoleButton.ToolTipMessage = LocaleHelper.GetLocaleString("Pseudoconsole");
        OpenSettingsButton.ToolTipMessage = LocaleHelper.GetLocaleString("Setup");
    }


    private void StateChangedActions()
    {
        ComponentWorkButton.IsHighlighted = IsRunning;
        ComponentWorkButton.Glyph = IsRunning ? "\uF8AE" : "\uF5B0";
    }

    private void SetupStateChanged()
    {
        ComponentWorkButton.Enabled = IsSetupComplete;
    }

    private async void OpenSettingsButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            EventHappens?.Invoke();

            await PipeHelper.SendOpenWindowPacket("MainWindow", new()
            {
                { "page", "Components.ViewComponentSettingsPage" },
                { "componentId", ComponentId },
            }, true);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ComponentUserControl), $"Cannot open the settings of {ComponentId}: {ex}");
        }
    }

    private async void OpenPseudoConsoleButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            EventHappens?.Invoke();

            await PipeHelper.SendOpenWindowPacket("ViewWindow", new()
            {
                { "id", ComponentId },
            }, true);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ComponentUserControl), $"Cannot open the pseudo console of {ComponentId}: {ex}");
        }
    }

    private void ComponentWorkButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            EventHappens?.Invoke();

            // The click toggles the state the component has when its turn comes in the queue, behind
            // the pipe commands that came first; the UI does not wait. Deciding from the shown state
            // made a quick double-click start the component twice (a restart) instead of
            // starting and stopping it.
            var id = ComponentId;
            // A start that still waits for the startup string from the GUI counts as running:
            // the second click of a double-click cancels it instead of asking for it again.
            ComponentCommandQueue.Post(id, async () =>
            {
                var task = await TasksHelper.Instance.GetTaskFromId(id);
                if (task != null && task.ProcessManager.CancelRequestedStart())
                    return;
                if (task != null && task.ProcessManager.IsRunningOrStartRequested)
                    await TasksHelper.Instance.StopTask(id);
                else
                    await TasksHelper.Instance.CreateAndRunNewTask(id);
            }, nameof(ComponentUserControl));
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(ComponentUserControl), $"Cannot change the state of {ComponentId}: {ex}");
        }
    }

    private void ComponentUserControl_Disposed(object? sender, EventArgs e)
    {
        DisconnectHandlers();
        this.Disposed -= ComponentUserControl_Disposed;
    }
}
