using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Helper.Items;
using CDPIUI.Shared;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigUtil;
using CDPIUI.Views.Main.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using ToolTip = Microsoft.UI.Xaml.Controls.ToolTip;
using ToolTipService = Microsoft.UI.Xaml.Controls.ToolTipService;
namespace CDPIUI.Controls.MainPage;

public sealed partial class ComponentTileUserControl
{
    private async Task<ProcessService> GetProcessManager()
    {
        return (await ComponentTasksManager.Instance.GetTaskFromId(StoreId))?.ProcessManager;
    }

    private async Task PreferTaskStateActions()
    {
        try
        {
            bool isRunning = await ComponentTasksManager.Instance.IsTaskRunning(StoreId);

            if (isRunning)
            {
                StateToggleButton.DisplayText = _localizer.GetLocalizedString("Stop");
                ShowComponentState(ComponentState.Running);
            }
            else
            {
                StateToggleButton.DisplayText = _localizer.GetLocalizedString("Start");
                _ = CheckComponentState(); // CheckComponentState catches and logs its own errors.
            }

            StateToggleButton.Checked = isRunning;
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }

    private async Task ToggleProcessButtonClick()
    {
        try
        {
            bool isRunning = await ComponentTasksManager.Instance.IsTaskRunning(StoreId);

            if (!isRunning)
            {
                _ = ComponentTasksManager.Instance.CreateAndRunNewTask(StoreId);
            }
            else
            {
                await ComponentTasksManager.Instance.StopTask(StoreId);
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }

    private async Task CheckComponentState()
    {
        try
        {
            ShowComponentState(await GetComponentState());
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }

    private async Task<ComponentState> GetComponentState()
    {
        ProcessService processManager = await GetProcessManager();
        if (processManager == null)
            return ComponentState.SetupRequired;

        if (processManager.IsErrorHappens)
        {
            return ComponentState.ExitedWithException;
        }
        else if (processManager.IsProcessRunning)
        {
            return ComponentState.Running;
        }
        else
        {
            return ComponentState.Stopped;
        }
    }

    private void ShowComponentState(ComponentState componentState)
    {
        ComponentStateVisual visual = ComponentStateVisual.For(componentState);
        if (visual == null)
            return;

        BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources[visual.BackgroundBrushKey];
        ForegroundStatusFontIcon.Glyph = visual.Glyph;
        ForegroundStatusFontIcon.FontSize = visual.FontSize;
        SetStatusToolTip(_localizer.GetLocalizedString(visual.ToolTipKey));
    }

    private void SetStatusToolTip(string text)
    {
        ToolTip toolTip = new();
        toolTip.Content = text;
        ToolTipService.SetToolTip(StatusGrid, toolTip);
    }

    private async Task ViewOutputButtonClick()
    {
        try
        {
            var window = await ((App)Application.Current).UnsafeCreateNewWindow<ViewWindow>(id: StoreId);
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }

    private async Task ViewSettingsButtonClick()
    {
        try
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();
            window.NavView_Navigate(typeof(ViewComponentSettingsPage),
                new NameValueCollection()
                    {
                        { "componentId", StoreId }
                    },
                    new DrillInNavigationTransitionInfo());
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ComponentTileUserControl), exception.ToString());
        }
    }
}
