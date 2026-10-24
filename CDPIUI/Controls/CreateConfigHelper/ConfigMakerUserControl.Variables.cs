using System;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CDPIUI.Controls.CreateConfigHelper;

// The preset variables list.
public sealed partial class ConfigMakerUserControl
{
    private async void CreateVariableButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigMakerVariableContentDialog dialog = new(_presetDocument)
            {
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary &&
                dialog.ResultVariable != null)
            {
                _presetDocument.Variables.Add(dialog.ResultVariable);
                AttachExistingPresetFiles();
                ToolsSelector.SelectIndex(1);
                RebuildPresetStructure();
                ScheduleDiagnostics();
            }
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private async void EditVariableButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as FrameworkElement)?.Tag is not ConfigMakerVariableDefinition variable)
            {
                return;
            }
            await EditVariableAsync(variable);
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private async Task EditVariableAsync(ConfigMakerVariableDefinition variable)
    {
        ConfigMakerVariableContentDialog dialog = new(_presetDocument, variable)
        {
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary ||
            dialog.ResultVariable == null)
        {
            return;
        }
        int index = _presetDocument.Variables.IndexOf(variable);
        if (index < 0)
        {
            return;
        }
        if (!string.Equals(dialog.OriginalName, dialog.ResultVariable.Name, StringComparison.Ordinal))
        {
            _presetDocument.ReplaceVariableReference(
                dialog.OriginalName,
                dialog.ResultVariable.Name);
            SetEditorText(_presetDocument.CommandText);
        }
        _presetDocument.Variables[index] = dialog.ResultVariable;
        AttachExistingPresetFiles();
        RebuildPresetStructure();
        ScheduleDiagnostics();
    }

    private async void RemoveVariableButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as FrameworkElement)?.Tag is not ConfigMakerVariableDefinition variable)
            {
                return;
            }
            bool referenced = CommandText.Contains(variable.Reference, StringComparison.OrdinalIgnoreCase);
            if (referenced)
            {
                ContentDialog confirmation = new()
                {
                    XamlRoot = XamlRoot,
                    Title = _localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogTitle"),
                    Content = _localizer.GetLocalizedString("ConfigMakerRemoveReferencedVariableDialogMessage"),
                    PrimaryButtonText = _localizer.GetLocalizedString("Remove"),
                    CloseButtonText = _localizer.GetLocalizedString("Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await confirmation.ShowQueuedAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
            }
            _presetDocument.Variables.Remove(variable);
            RebuildPresetStructure();
            ScheduleDiagnostics();
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private void PresetVariablesListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!IsEditorReadOnly &&
            PresetVariablesListView.SelectedItem is ConfigMakerVariableDefinition variable)
        {
            InsertAtCursor(variable.Reference);
        }
    }
}
