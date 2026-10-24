using System.Threading.Tasks;
using CDPIUI.Shared.ConditionalLaunch;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConditionalLaunch;

/// <summary>What <see cref="ConditionalLaunchViewModel"/> needs from the task list window.</summary>
public interface IConditionalLaunchView
{
    /// <summary>Shows the animated status bar that hides after a few seconds.</summary>
    void ShowStatus(string message, InfoBarSeverity severity);

    Task<bool> ConfirmDeleteAsync(string taskName);

    /// <summary>Returns the chosen task file, or null when the user canceled.</summary>
    string? PickImportFile();

    /// <summary>Returns the chosen export path, or null when the user canceled.</summary>
    string? PickExportFile(string suggestedFileName);

    /// <summary>Opens the editor of the task, or of a new task when <paramref name="task"/> is null.</summary>
    Task OpenEditorAsync(ConditionalTask? task, string tasksDirectory);

    Task OpenImportedTaskEditorAsync(ConditionalTask task, string tasksDirectory);

    void CloseEditor(string taskId);
}
