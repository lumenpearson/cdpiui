using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.ConditionalLaunch;
using CDPIUI.Core;
using CDPIUI.Core.Communication;
using CDPIUI.Shared.ConditionalLaunch;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.ViewModels.ConditionalLaunch;

/// <summary>
/// The list of conditional launch tasks, the preview of the selected task and the task
/// commands (run, enable, export, delete).
/// </summary>
public sealed partial class ConditionalLaunchViewModel : ObservableObject, IDisposable
{
    private const string LogSource = "ConditionalLaunchWindow";

    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly IConditionalLaunchView _view;
    private readonly string _tasksDirectory;
    private ConditionalTask? _selectedTask;
    private object? _selectedTaskItem;
    private bool _isEmptyTaskListVisible = true;
    private bool _isTaskListVisible = true;
    private bool _isNoSelectionVisible = true;
    private bool _isPreviewVisible;
    private string _previewName = string.Empty;
    private string _previewStatus = string.Empty;
    private string _previewPriority = string.Empty;
    private string _previewFile = string.Empty;

    public ConditionalLaunchViewModel(IConditionalLaunchView view)
    {
        _view = view;
        NewTaskCommand = new AsyncRelayCommand(() => _view.OpenEditorAsync(null, _tasksDirectory));
        EditTaskCommand = new AsyncRelayCommand(EditSelectedTaskAsync, () => HasSelection);
        DeleteTaskCommand = new AsyncRelayCommand(DeleteSelectedTaskAsync, () => HasSelection);
        RunTaskCommand = new AsyncRelayCommand(RunSelectedTaskAsync, () => HasSelection);
        ExportTaskCommand = new RelayCommand(ExportSelectedTask, () => HasSelection);
        ImportTaskCommand = new AsyncRelayCommand(ImportTaskAsync);
        ReloadCommand = new RelayCommand(() => LoadTasks());
        ToggleTaskEnabledCommand = new RelayCommand(ToggleSelectedTaskEnabled);

        _tasksDirectory = ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
            SettingsManager.Instance.SettingsFilePath);

        LoadTasks();

        ConditionalTaskEditorWindow.TaskSaved += ConditionalTaskEditorWindow_TaskSaved;
    }

    internal ObservableCollection<ConditionalTaskListItem> Tasks { get; } = [];
    internal ObservableCollection<ConditionalTriggerListItem> PreviewTriggers { get; } = [];
    internal ObservableCollection<ConditionalActionListItem> PreviewActions { get; } = [];

    public AsyncRelayCommand NewTaskCommand { get; }
    public AsyncRelayCommand EditTaskCommand { get; }
    public AsyncRelayCommand DeleteTaskCommand { get; }
    public AsyncRelayCommand RunTaskCommand { get; }
    public RelayCommand ExportTaskCommand { get; }
    public AsyncRelayCommand ImportTaskCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public RelayCommand ToggleTaskEnabledCommand { get; }

    /// <summary>The task shown in the preview; null when nothing is selected.</summary>
    public ConditionalTask? SelectedTask => _selectedTask;

    public bool HasSelection => _selectedTask != null;

    /// <summary>
    /// The selected item of the task list. The list shows it through a one-way binding and
    /// reports the user's selection from its SelectionChanged handler.
    /// </summary>
    public object? SelectedTaskItem
    {
        get => _selectedTaskItem;
        set
        {
            if (!SetProperty(ref _selectedTaskItem, value))
                return;
            UpdatePreview((value as ConditionalTaskListItem)?.Task);
            UpdateSelectionCommands();
        }
    }

    public bool IsEmptyTaskListVisible
    {
        get => _isEmptyTaskListVisible;
        private set => SetProperty(ref _isEmptyTaskListVisible, value);
    }

    public bool IsTaskListVisible
    {
        get => _isTaskListVisible;
        private set => SetProperty(ref _isTaskListVisible, value);
    }

    public bool IsNoSelectionVisible
    {
        get => _isNoSelectionVisible;
        private set => SetProperty(ref _isNoSelectionVisible, value);
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        private set => SetProperty(ref _isPreviewVisible, value);
    }

    public string PreviewName
    {
        get => _previewName;
        private set => SetProperty(ref _previewName, value);
    }

    public string PreviewStatus
    {
        get => _previewStatus;
        private set => SetProperty(ref _previewStatus, value);
    }

    public string PreviewPriority
    {
        get => _previewPriority;
        private set => SetProperty(ref _previewPriority, value);
    }

    public string PreviewFile
    {
        get => _previewFile;
        private set => SetProperty(ref _previewFile, value);
    }

    public void Dispose()
    {
        ConditionalTaskEditorWindow.TaskSaved -= ConditionalTaskEditorWindow_TaskSaved;
    }

    /// <summary>Text of the context menu item that enables or disables the selected task.</summary>
    public string GetToggleEnabledText() =>
        Text(_selectedTask?.IsEnabled == true ? "CL_DisableTaskMenuItem" : "CL_EnableTaskMenuItem");

    private void ConditionalTaskEditorWindow_TaskSaved(string taskId, bool isImport)
    {
        LoadTasks(taskId);
        _view.ShowStatus(Text(isImport ? "CL_TaskImported" : "CL_TaskSaved"), InfoBarSeverity.Success);
    }

    public void LoadTasks(string? selectedTaskId = null)
    {
        selectedTaskId ??= _selectedTask?.Id;
        var previouslyLoadedTaskIds = Tasks
            .Select(item => item.Task.Id)
            .ToArray();
        Tasks.Clear();

        Directory.CreateDirectory(_tasksDirectory);
        var invalidFiles = 0;
        var loadedTasks = Directory.EnumerateFiles(
                _tasksDirectory,
                $"*{ConditionalTaskFileService.FileExtension}")
            .Select(filePath =>
            {
                try
                {
                    return ConditionalTaskFileService.Load(filePath);
                }
                catch
                {
                    invalidFiles++;
                    return null;
                }
            })
            .Where(task => task != null)
            .Cast<ConditionalTask>()
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.Name, StringComparer.CurrentCultureIgnoreCase);

        foreach (var task in loadedTasks)
            Tasks.Add(ConditionalLaunchUiCatalog.CreateTaskListItem(task, _localizer));

        var loadedTaskIds = Tasks
            .Select(item => item.Task.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removedTaskId in previouslyLoadedTaskIds.Where(id => !loadedTaskIds.Contains(id)))
            _view.CloseEditor(removedTaskId);

        IsEmptyTaskListVisible = Tasks.Count == 0;
        IsTaskListVisible = Tasks.Count != 0;

        var selected = Tasks.FirstOrDefault(item =>
                string.Equals(item.Task.Id, selectedTaskId, StringComparison.OrdinalIgnoreCase))
            ?? Tasks.FirstOrDefault();
        SelectedTaskItem = selected;
        UpdatePreview(selected?.Task);

        if (invalidFiles > 0)
        {
            _view.ShowStatus(
                string.Format(Text("CL_InvalidFilesSkippedFormat"), invalidFiles),
                InfoBarSeverity.Warning);
        }

        UpdateSelectionCommands();
    }

    private Task EditSelectedTaskAsync() =>
        _selectedTask == null ? Task.CompletedTask : _view.OpenEditorAsync(_selectedTask, _tasksDirectory);

    private void ToggleSelectedTaskEnabled()
    {
        if (_selectedTask == null)
            return;

        var taskId = _selectedTask.Id;
        var taskName = _selectedTask.Name;
        var isEnabled = !_selectedTask.IsEnabled;
        try
        {
            _selectedTask.IsEnabled = isEnabled;
            ConditionalTaskFileService.Save(
                _selectedTask,
                _tasksDirectory,
                _selectedTask.FilePath);
            LoadTasks(taskId);
            NotifyTaskEngine();
            _view.ShowStatus(
                string.Format(Text(isEnabled
                    ? "CL_TaskEnabledFormat"
                    : "CL_TaskDisabledFormat"), taskName),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _selectedTask.IsEnabled = !isEnabled;
            _view.ShowStatus(
                string.Format(Text("CL_CannotChangeTaskStateFormat"), ex.Message),
                InfoBarSeverity.Error);
        }
    }

    private async Task RunSelectedTaskAsync()
    {
        if (_selectedTask == null)
            return;

        try
        {
            await PipeHelper.SendConditionalTaskExecutePacket(_selectedTask.Id);
            _view.ShowStatus(
                string.Format(Text("CL_TaskExecutionRequestedFormat"), _selectedTask.Name),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _view.ShowStatus(
                string.Format(Text("CL_CannotRunTaskFormat"), ex.Message),
                InfoBarSeverity.Error);
        }
    }

    private void UpdatePreview(ConditionalTask? task)
    {
        _selectedTask = task;
        PreviewTriggers.Clear();
        PreviewActions.Clear();
        IsNoSelectionVisible = task == null;
        IsPreviewVisible = task != null;
        if (task == null)
            return;

        var item = ConditionalLaunchUiCatalog.CreateTaskListItem(task, _localizer);
        PreviewName = item.Name;
        PreviewStatus = item.Status;
        PreviewPriority = item.PriorityLabel;
        PreviewFile = task.FilePath ?? Text("CL_NotSaved");

        foreach (var trigger in task.Triggers)
        {
            var type = Text(trigger.Type switch
            {
                ConditionalTriggerType.HotKey => "CL_TriggerHotKey",
                ConditionalTriggerType.ProcessStarted => "CL_TriggerProcessStarted",
                _ => "CL_TriggerProcessStopped"
            });
            PreviewTriggers.Add(new ConditionalTriggerListItem(
                type,
                ConditionalLaunchUiCatalog.FormatTrigger(trigger, _localizer),
                Text("CL_StatusConfigured")));
        }

        var definitions = ConditionalLaunchUiCatalog.CreateActionDefinitions(_localizer);
        for (var index = 0; index < task.Actions.Count; index++)
        {
            PreviewActions.Add(ConditionalLaunchUiCatalog.CreateActionListItem(
                task.Actions[index], index + 1, _localizer, definitions));
        }
    }

    private async Task ImportTaskAsync()
    {
        string? fileName = _view.PickImportFile();
        if (fileName == null)
            return;

        try
        {
            var imported = ConditionalTaskFileService.Load(fileName);
            if (Tasks.Any(item => string.Equals(
                item.Task.Id,
                imported.Id,
                StringComparison.OrdinalIgnoreCase)))
            {
                imported.Id = Guid.NewGuid().ToString("D");
            }

            imported.FilePath = null;
            imported.IsEnabled = false;
            await _view.OpenImportedTaskEditorAsync(imported, _tasksDirectory);
        }
        catch (Exception ex)
        {
            _view.ShowStatus(
                string.Format(Text("CL_CannotImportTaskFormat"), ex.Message),
                InfoBarSeverity.Error);
        }
    }

    private void ExportSelectedTask()
    {
        if (_selectedTask == null)
            return;

        try
        {
            string? fileName = _view.PickExportFile(
                SanitizeFileName(_selectedTask.Name) + ConditionalTaskFileService.FileExtension);
            if (fileName == null)
                return;

            ConditionalTaskFileService.Export(_selectedTask, fileName);
            _view.ShowStatus(Text("CL_TaskExported"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _view.ShowStatus(string.Format(Text("CL_CannotExportTaskFormat"), ex.Message), InfoBarSeverity.Error);
        }
    }

    private async Task DeleteSelectedTaskAsync()
    {
        if (_selectedTask?.FilePath == null)
            return;

        if (!await _view.ConfirmDeleteAsync(_selectedTask.Name))
            return;

        try
        {
            var deletedTaskId = _selectedTask.Id;
            File.Delete(_selectedTask.FilePath);
            _view.CloseEditor(deletedTaskId);
            LoadTasks();
            NotifyTaskEngine();
            _view.ShowStatus(Text("CL_TaskDeleted"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _view.ShowStatus(string.Format(Text("CL_CannotDeleteTaskFormat"), ex.Message), InfoBarSeverity.Error);
        }
    }

    private void UpdateSelectionCommands()
    {
        OnPropertyChanged(nameof(HasSelection));
        EditTaskCommand.NotifyCanExecuteChanged();
        RunTaskCommand.NotifyCanExecuteChanged();
        ExportTaskCommand.NotifyCanExecuteChanged();
        DeleteTaskCommand.NotifyCanExecuteChanged();
    }

    private static void NotifyTaskEngine()
    {
        _ = PipeHelper.SendConditionalTasksReloadPacket();
    }

    private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);

    internal static string SanitizeFileName(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return string.IsNullOrWhiteSpace(value) ? "ConditionalTask" : value;
    }
}
