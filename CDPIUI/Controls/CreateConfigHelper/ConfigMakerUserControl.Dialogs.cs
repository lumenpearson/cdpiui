using System.Collections.Generic;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using CDPIUI.ViewModels.ConfigMaker;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CDPIUI.Controls.CreateConfigHelper;

// The dialogs, the file pickers and the clipboard the view model asks for.
public sealed partial class ConfigMakerUserControl
{
    void IConfigMakerView.CopyToClipboard(string text)
    {
        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    async Task<bool> IConfigMakerView.ConfirmAsync(string title, string message, string primaryButtonText)
    {
        ContentDialog confirmation = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = _localizer.GetLocalizedString("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await confirmation.ShowQueuedAsync() == ContentDialogResult.Primary;
    }

    async Task<ConfigMakerSavePresetRequest> IConfigMakerView.AskSavePresetAsync(
        string suggestedName,
        bool overwriteEnabled)
    {
        ConfigMakerSavePresetContentDialog dialog = new(suggestedName)
        {
            XamlRoot = XamlRoot,
            OverwriteEnable = overwriteEnabled,
        };
        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary)
        {
            return null;
        }
        return new ConfigMakerSavePresetRequest(
            dialog.PresetName,
            dialog.Result == ConfigMakerSavePresetContentDialogResult.Overwrite);
    }

    async Task<ConfigMakerVariableEditResult> IConfigMakerView.EditVariableAsync(
        ConfigMakerPresetDocument document,
        ConfigMakerVariableDefinition variable)
    {
        ConfigMakerVariableContentDialog dialog = new(document, variable)
        {
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary ||
            dialog.ResultVariable == null)
        {
            return null;
        }
        return new ConfigMakerVariableEditResult(dialog.ResultVariable, dialog.OriginalName);
    }

    async Task<ConfigMakerFileReplacementChoice> IConfigMakerView.AskPresetFileReplacementAsync(string missingPath)
    {
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = string.Format(
                _localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"),
                missingPath),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = _localizer.GetLocalizedString("ConfigMakerPresetFileReplacementChoiceMessage"),
            TextWrapping = TextWrapping.Wrap,
        });
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _localizer.GetLocalizedString("ConfigMakerPresetFileReplaceDialogTitle"),
            Content = content,
            PrimaryButtonText = _localizer.GetLocalizedString("ConfigMakerPresetFileApplyAutoCorrectionButton"),
            SecondaryButtonText = _localizer.GetLocalizedString("ConfigMakerPresetFileChooseManuallyButton"),
            CloseButtonText = _localizer.GetLocalizedString("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowQueuedAsync() switch
        {
            ContentDialogResult.Primary => ConfigMakerFileReplacementChoice.AutoCorrect,
            ContentDialogResult.Secondary => ConfigMakerFileReplacementChoice.ChooseManually,
            _ => ConfigMakerFileReplacementChoice.Cancel,
        };
    }

    async Task<bool> IConfigMakerView.AskTextFileApplicationAsync(string path)
    {
        EditSitelistAskApplicationContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            FilePath = path,
        };
        await dialog.ShowQueuedAsync();
        return dialog.IsSuccess;
    }

    string IConfigMakerView.ChooseReplacementFile(string missingPath) =>
        ConfigMakerFileDialogs.ChooseReplacementFile(_localizer, missingPath);

    IReadOnlyList<string> IConfigMakerView.ChoosePresetFilesToAdd() =>
        ConfigMakerFileDialogs.ChoosePresetFiles(_localizer);

    string IConfigMakerView.ChooseExportPath(bool allowTextFormat) =>
        ConfigMakerFileDialogs.ChooseExportPath(_localizer, allowTextFormat);
}
