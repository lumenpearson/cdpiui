using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CDPIUI.Controls.CreateConfigHelper;

// The command reference panel built from the component help output.
public sealed partial class ConfigMakerUserControl
{
    public async Task RefreshHelpAsync()
    {
        if (!string.IsNullOrWhiteSpace(ComponentId))
        {
            await LoadHelpAsync(ComponentId, forceRefresh: true);
        }
    }

    private async Task LoadHelpAsync(string componentId, bool forceRefresh)
    {
        _helpCancellation?.Cancel();
        _helpCancellation?.Dispose();
        _helpCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _helpCancellation.Token;

        HelpProgressRing.IsActive = true;
        CommandListView.IsEnabled = false;
        UsageTextBlock.Text = _localizer.GetLocalizedString("ConfigMakerLoadingHelpMessage");
        try
        {
            ComponentCommandHelpDocument document = await _helpService.LoadAsync(
                componentId,
                cancellationToken,
                forceRefresh);
            if (cancellationToken.IsCancellationRequested ||
                !string.Equals(ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _helpDocument = document;
            UsageTextBlock.Text = !string.IsNullOrWhiteSpace(document.Usage)
                ? document.Usage
                : string.Format(
                    _localizer.GetLocalizedString("ConfigMakerHelpOptionsLoadedMessage"),
                    document.Options.Count);
            RebuildCommandModules();
            RebuildCommandOptions();
            RebuildPresetStructure();
            RebuildDiagnostics();
            ShowHelpLoadStatus(document);
        }
        catch (OperationCanceledException)
        {
            // The load was cancelled (a newer request or the control was unloaded): nothing is left to show.
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                HelpProgressRing.IsActive = false;
                CommandListView.IsEnabled = true;
            }
        }
    }

    private void ShowHelpLoadStatus(ComponentCommandHelpDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Error))
        {
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerHelpLoadFailedMessage"),
                    document.Error),
                InfoBarSeverity.Warning);
        }
        else if (document.Options.Count == 0)
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerHelpParseEmptyMessage"),
                InfoBarSeverity.Warning);
        }
        else
        {
            EditorInfoBar.IsOpen = false;
        }
    }

    private void RebuildCommandOptions()
    {
        string query = CommandSearchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<ComponentCommandHelpOption> filtered = _helpDocument.Options;
        if (ModuleComboBox.SelectedItem is ConfigMakerCommandModuleViewModel module && !module.IsAll)
        {
            filtered = filtered.Where(option =>
                string.Equals(option.GroupName, module.GroupName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(option =>
                option.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Syntax.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        CommandOptions.Clear();
        foreach (ComponentCommandHelpOption option in filtered
                     .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            CommandOptions.Add(new ConfigMakerCommandOptionViewModel
            {
                Source = option,
                Description = string.IsNullOrWhiteSpace(option.Description)
                    ? _localizer.GetLocalizedString("ConfigMakerNoDescriptionMessage")
                    : option.Description,
            });
        }
    }

    private void RebuildCommandModules()
    {
        string previousGroup = (ModuleComboBox.SelectedItem as ConfigMakerCommandModuleViewModel)?.GroupName;
        bool previousWasAll = (ModuleComboBox.SelectedItem as ConfigMakerCommandModuleViewModel)?.IsAll != false;
        List<string> namedGroups = _helpDocument.Options
            .Select(option => option.GroupName)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CommandModules.Clear();
        CommandModules.Add(new ConfigMakerCommandModuleViewModel
        {
            DisplayName = _localizer.GetLocalizedString("ConfigMakerAllModules"),
            IsAll = true,
        });

        if (namedGroups.Count > 0 && _helpDocument.Options.Any(option => string.IsNullOrWhiteSpace(option.GroupName)))
        {
            CommandModules.Add(new ConfigMakerCommandModuleViewModel
            {
                DisplayName = _localizer.GetLocalizedString("ConfigMakerGeneralModule"),
                GroupName = string.Empty,
            });
        }

        foreach (string group in namedGroups)
        {
            CommandModules.Add(new ConfigMakerCommandModuleViewModel
            {
                DisplayName = group,
                GroupName = group,
            });
        }

        ModuleComboBox.Visibility = namedGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ModuleComboBox.SelectedItem = previousWasAll
            ? CommandModules[0]
            : CommandModules.FirstOrDefault(item =>
                !item.IsAll && string.Equals(item.GroupName, previousGroup, StringComparison.OrdinalIgnoreCase))
                ?? CommandModules[0];
    }

    private void ModuleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RebuildCommandOptions();

    private void CommandSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RebuildCommandOptions();
        }
    }

    private void CommandListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCommand = CommandListView.SelectedItem as ConfigMakerCommandOptionViewModel;
        if (_selectedCommand == null)
        {
            CommandDetailsBorder.Visibility = Visibility.Collapsed;
            CopyButton.IsEnabled = false;
            InsertButton.IsEnabled = false;
            Grid.SetRowSpan(CommandListView, 2);
            return;
        }

        ComponentCommandHelpOption option = _selectedCommand.Source;
        SelectedCommandNameTextBlock.Text = option.DisplayName;
        SelectedCommandSyntaxTextBlock.Text = option.Syntax;
        SelectedCommandDescriptionTextBlock.Text = _selectedCommand.Description;

        bool hasArgument = !string.IsNullOrWhiteSpace(option.ArgumentPlaceholder);
        CommandArgumentPanel.Visibility = hasArgument ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.IsEnabled = true;
        InsertButton.IsEnabled = !IsEditorReadOnly;

        CommandArgumentTextBox.Text = string.Empty;
        CommandArgumentTextBox.PlaceholderText = option.ArgumentPlaceholder;
        CommandArgumentLabelTextBlock.Text = hasArgument
            ? string.Format(
                _localizer.GetLocalizedString(
                    option.IsArgumentRequired
                        ? "ConfigMakerRequiredArgumentLabel"
                        : "ConfigMakerOptionalArgumentLabel"),
                option.ArgumentPlaceholder)
            : string.Empty;
        CommandDetailsBorder.Visibility = Visibility.Visible;
        Grid.SetRowSpan(CommandListView, 1);
    }

    private void InsertCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditorReadOnly)
        {
            return;
        }
        string command = BuildSelectedCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        InsertAtCursor(command);
    }

    private void CopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        string command = BuildSelectedCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(command);
        Clipboard.SetContent(package);
    }

    private string BuildSelectedCommand()
    {
        if (_selectedCommand == null)
        {
            return string.Empty;
        }

        string command = ConfigMakerCommandText.BuildOptionCommand(
            _selectedCommand.Source,
            CommandArgumentTextBox.Text);
        if (command == null)
        {
            ShowEditorMessage(
                _localizer.GetLocalizedString("ConfigMakerRequiredArgumentMissingMessage"),
                InfoBarSeverity.Warning);
            return string.Empty;
        }
        return command;
    }
}
