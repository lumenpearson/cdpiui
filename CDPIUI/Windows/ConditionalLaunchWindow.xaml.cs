using System;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Default;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.ViewModels.ConditionalLaunch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using WinUI3Localizer;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace CDPIUI;

public sealed partial class ConditionalLaunchWindow : TemplateWindow, IConditionalLaunchView
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly DispatcherTimer _statusInfoBarTimer = new()
    {
        Interval = TimeSpan.FromSeconds(5)
    };
    private bool _allowInfoBarClose;

    public ConditionalLaunchViewModel ViewModel { get; }

    public ConditionalLaunchWindow()
    {
        InitializeComponent();

        WindowTitle = Text("CL_WindowTitle");
        IconUri = @"Assets/Icons/ConditionalUtil.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        WindowMinSize = new System.Windows.Size(900, 580);
        _statusInfoBarTimer.Tick += StatusInfoBarTimer_Tick;
        HideInfoBarAnimation.Completed += (_, _) =>
        {
            _allowInfoBarClose = true;
            StatusInfoBar.IsOpen = false;
            _allowInfoBarClose = false;
        };

        ViewModel = new ConditionalLaunchViewModel(this);

        this.Closed += ConditionalLaunchWindow_Closed;
        this.SizeChanged += ConditionalLaunchWindow_SizeChanged;
    }

    public void ReloadTasksFromStorage()
    {
        ViewModel.LoadTasks();
    }

    private void ConditionalLaunchWindow_Closed(object sender, WindowEventArgs args)
    {
        _statusInfoBarTimer.Stop();
        ViewModel.Dispose();
        this.SizeChanged -= ConditionalLaunchWindow_SizeChanged;
        this.Closed -= ConditionalLaunchWindow_Closed;
    }

    private void ConditionalLaunchWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        TasksBorder.MaxHeight = this.Height - 400;
    }

    private void TaskListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedTaskItem = TaskListView.SelectedItem;
    }

    private async void TaskListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.HasSelection)
        {
            e.Handled = true;
            await ViewModel.EditTaskCommand.ExecuteAsync(null);
        }
    }

    private async void TaskListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.HasSelection)
        {
            e.Handled = true;
            await ViewModel.EditTaskCommand.ExecuteAsync(null);
        }
    }

    private void TaskListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        DependencyObject? current = source;
        while (current != null && current is not ListViewItem)
            current = VisualTreeHelper.GetParent(current);
        if (current is not ListViewItem container)
        {
            return;
        }

        TaskListView.SelectedItem = container.Content;
        if (RootGrid.Resources["TaskContextMenu"] is MenuFlyout menu)
        {
            foreach (MenuFlyoutItem item in menu.Items.OfType<MenuFlyoutItem>())
            {
                item.Command = item.Tag switch
                {
                    "Run" => ViewModel.RunTaskCommand,
                    "ToggleEnabled" => ViewModel.ToggleTaskEnabledCommand,
                    "Edit" => ViewModel.EditTaskCommand,
                    "Delete" => ViewModel.DeleteTaskCommand,
                    _ => item.Command,
                };
            }

            var toggleItem = menu.Items
                .OfType<MenuFlyoutItem>()
                .FirstOrDefault(item => Equals(item.Tag, "ToggleEnabled"));
            if (toggleItem != null && ViewModel.HasSelection)
                toggleItem.Text = ViewModel.GetToggleEnabledText();

            menu.ShowAt(RootGrid, new FlyoutShowOptions
            {
                Position = e.GetPosition(RootGrid)
            });
        }
        e.Handled = true;
    }

    private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string helpUrl })
        {
            Commands.CommandsHandler.HandleCommand(
                $"cdpiui://Help/{helpUrl.Trim('/')}/");
        }
    }

    async Task IConditionalLaunchView.OpenEditorAsync(ConditionalTask? task, string tasksDirectory)
    {
        var app = (App)Application.Current;
        var windowId = task == null
            ? ConditionalTaskEditorWindow.NewTaskWindowId
            : ConditionalTaskEditorWindow.WindowIdPrefix + task.Id;
        var editor = await app.UnsafeCreateNewWindow<ConditionalTaskEditorWindow>(
            activate: false,
            id: windowId);
        editor.SetTask(task, tasksDirectory);

        var cornerOffset = (int)Math.Round(
            16 * WindowsPositionHelper.GetScaleFactor(this));
        WindowsPositionHelper.SetWindowPosition(
            editor,
            AppWindow.Position.X + cornerOffset,
            AppWindow.Position.Y + cornerOffset);
        WindowsPositionHelper.SetWindowOwner(editor, this);
        App.ActivateWindow(editor);
    }

    async Task IConditionalLaunchView.OpenImportedTaskEditorAsync(ConditionalTask task, string tasksDirectory)
    {
        var editor = await ((App)Application.Current)
            .UnsafeCreateNewWindow<ConditionalTaskEditorWindow>(
                activate: false,
                id: ConditionalTaskEditorWindow.WindowIdPrefix + task.Id);
        editor.SetTask(task, tasksDirectory, isImport: true);
        App.ActivateWindow(editor);
    }

    void IConditionalLaunchView.CloseEditor(string taskId)
    {
        ((App)Application.Current).CloseWindow<ConditionalTaskEditorWindow>(
            ConditionalTaskEditorWindow.WindowIdPrefix + taskId);
    }

    string? IConditionalLaunchView.PickImportFile()
    {
        using OpenFileDialog dialog = new()
        {
            Title = Text("CL_ImportDialogTitle"),
            Filter = Text("CL_TaskFileFilter"),
            Multiselect = false
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.FileName
            : null;
    }

    string? IConditionalLaunchView.PickExportFile(string suggestedFileName)
    {
        using SaveFileDialog dialog = new()
        {
            Title = Text("CL_ExportDialogTitle"),
            Filter = Text("CL_TaskFileFilter"),
            FileName = suggestedFileName
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.FileName
            : null;
    }

    async Task<bool> IConditionalLaunchView.ConfirmDeleteAsync(string taskName)
    {
        ContentDialog dialog = new()
        {
            Title = Text("CL_DeleteDialogTitle"),
            Content = string.Format(Text("CL_DeleteDialogContentFormat"), taskName),
            PrimaryButtonText = Text("CL_DeleteButtonText"),
            CloseButtonText = Text("CL_CancelButtonText"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    void IConditionalLaunchView.ShowStatus(string message, InfoBarSeverity severity)
    {
        _statusInfoBarTimer.Stop();
        ShowInfoBarAnimation.Stop();
        HideInfoBarAnimation.Stop();
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
        ShowInfoBarAnimation.Begin();
        _statusInfoBarTimer.Start();
    }

    private void StatusInfoBarTimer_Tick(object? sender, object e)
    {
        HideStatusInfoBar();
    }

    private void StatusInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        if (_allowInfoBarClose)
            return;

        HideStatusInfoBar();
        args.Cancel = true;
    }

    private void HideStatusInfoBar()
    {
        _statusInfoBarTimer.Stop();
        ShowInfoBarAnimation.Stop();
        HideInfoBarAnimation.Begin();
    }

    private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);
}
