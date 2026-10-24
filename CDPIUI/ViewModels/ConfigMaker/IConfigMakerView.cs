using System.Collections.Generic;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels.ConfigMaker;

/// <summary>The work of <see cref="ConfigMakerViewModel"/> that needs the editor control, a dialog or a window.</summary>
public interface IConfigMakerView
{
    bool IsViewLoaded { get; }

    string EditorText { get; }

    /// <summary>Replaces the editor text and returns the text the editor holds afterwards.</summary>
    string ReplaceEditorText(string text);

    (int Line, int Column) GetCursorPosition();

    void SetCursorPosition(int line, int column, bool focus);

    void ClearEditorUndoHistory();

    void RefreshEditorHighlighting();

    void SetOutputComponent(string componentId);

    Task RefreshOutputAsync();

    void SetPresetFilesPanelVisible(bool visible);

    void ShowVariablesTool();

    void CopyToClipboard(string text);

    Task<bool> ConfirmAsync(string title, string message, string primaryButtonText);

    /// <summary>Returns <see langword="null"/> when the user cancels.</summary>
    Task<ConfigMakerSavePresetRequest> AskSavePresetAsync(string suggestedName, bool overwriteEnabled);

    /// <summary>
    /// Creates a variable when <paramref name="variable"/> is <see langword="null"/>.
    /// Returns <see langword="null"/> when the user cancels.
    /// </summary>
    Task<ConfigMakerVariableEditResult> EditVariableAsync(
        ConfigMakerPresetDocument document,
        ConfigMakerVariableDefinition variable);

    Task<ConfigMakerFileReplacementChoice> AskPresetFileReplacementAsync(string missingPath);

    /// <summary>Asks which application opens a text file; returns whether the dialog succeeded.</summary>
    Task<bool> AskTextFileApplicationAsync(string path);

    /// <summary>Returns an empty string when the user cancels.</summary>
    string ChooseReplacementFile(string missingPath);

    /// <summary>Returns an empty list when the user cancels.</summary>
    IReadOnlyList<string> ChoosePresetFilesToAdd();

    /// <summary>Returns <see langword="null"/> when the user cancels.</summary>
    string ChooseExportPath(bool allowTextFormat);
}
