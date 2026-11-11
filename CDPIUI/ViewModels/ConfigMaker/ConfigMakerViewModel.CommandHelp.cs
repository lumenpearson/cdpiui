using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The command reference panel built from the component help output.
public sealed partial class ConfigMakerViewModel
{
    private readonly ComponentCommandHelpService _helpService = new();
    private readonly ConfigMakerCommandHelpPanel _commandHelpPanel;
    private readonly ConfigMakerCommandHelpLoader _helpLoader;

    public ObservableCollection<ConfigMakerCommandOptionViewModel> CommandOptions => _commandHelpPanel.CommandOptions;
    public ObservableCollection<ConfigMakerCommandModuleViewModel> CommandModules => _commandHelpPanel.CommandModules;
    public RelayCommand CopyCommandCommand { get; }
    public RelayCommand InsertCommandCommand { get; }

    /// <summary>The module filter; <see cref="object"/> because the combo box binds it two-way.</summary>
    public object SelectedCommandModule
    {
        get => _commandHelpPanel.SelectedCommandModule;
        set => _commandHelpPanel.SelectedCommandModule = value;
    }

    public string CommandSearchText
    {
        get => _commandHelpPanel.CommandSearchText;
        set => _commandHelpPanel.CommandSearchText = value;
    }

    public bool IsHelpLoading => _helpLoader.IsHelpLoading;

    public bool IsCommandListEnabled => _helpLoader.IsCommandListEnabled;

    public bool HasCommandModules => _commandHelpPanel.HasCommandModules;

    public string UsageText => _helpLoader.UsageText;

    public bool HasSelectedCommand => _commandHelpPanel.HasSelectedCommand;
    public bool CanInsertCommand => _commandHelpPanel.CanInsertCommand;
    public bool HasCommandArgument => _commandHelpPanel.HasCommandArgument;
    public string SelectedCommandName => _commandHelpPanel.SelectedCommandName;
    public string SelectedCommandSyntax => _commandHelpPanel.SelectedCommandSyntax;
    public string SelectedCommandDescription => _commandHelpPanel.SelectedCommandDescription;
    public string CommandArgumentPlaceholder => _commandHelpPanel.CommandArgumentPlaceholder;
    public string CommandArgumentLabel => _commandHelpPanel.CommandArgumentLabel;

    public string CommandArgumentText
    {
        get => _commandHelpPanel.CommandArgumentText;
        set => _commandHelpPanel.CommandArgumentText = value;
    }

    public async Task RefreshHelpAsync()
    {
        if (!string.IsNullOrWhiteSpace(ComponentId))
        {
            await LoadHelpAsync(ComponentId, forceRefresh: true);
        }
    }

    /// <summary>Called by the view when the selection of the command list changed.</summary>
    public void SelectCommand(ConfigMakerCommandOptionViewModel command)
    {
        _commandHelpPanel.SelectCommand(command);
        CopyCommandCommand.NotifyCanExecuteChanged();
        InsertCommandCommand.NotifyCanExecuteChanged();
    }

    private Task LoadHelpAsync(string componentId, bool forceRefresh) =>
        _helpLoader.LoadAsync(componentId, forceRefresh);

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
            IsStatusOpen = false;
        }
    }

    private void InsertSelectedCommand()
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

    private void CopySelectedCommand()
    {
        string command = BuildSelectedCommand();
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        _view.CopyToClipboard(command);
    }

    private string BuildSelectedCommand()
    {
        string command = _commandHelpPanel.BuildSelectedCommand();
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
