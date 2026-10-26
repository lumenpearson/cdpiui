using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Views.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>
/// What <see cref="CreateNewConfigViewModel"/> needs from the page: the text editor, the
/// editor tabs, dialogs and navigation.
/// </summary>
public interface ICreateNewConfigView
{
    /// <summary>The text of the startup string editor.</summary>
    string EditorText { get; set; }

    void ResetEditorCursor();

    bool IsTextEditorTabSelected { get; }

    void SelectDesignerTab();

    void SelectTextEditorTab();

    void ApplyEditorBackground();

    void CloseSearch();

    /// <summary>Sets whether leaving the page asks the user first.</summary>
    void SetExitAskAvailable(bool value);

    /// <summary>
    /// Asks which files the config uses. Returns the selected files, or
    /// <see langword="null"/> when the user cancelled.
    /// </summary>
    Task<Dictionary<string, string>> SelectUsedFilesAsync(List<string> usedFiles, ConfigItem configItem, string directory, AskAutoFillMode mode);

    Task ShowErrorAsync(string message, string title);

    /// <summary>Shows that the config was saved, then closes the window or leaves the page.</summary>
    Task ShowSaveCompletedAsync();

    void NavigateToMainPage();

    /// <summary>Returns the edited config to the page that opened the editor.</summary>
    void ReturnEditedConfig(ConfigItem configItem);
}
