using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.ViewModels.ConfigMaker;

// The command reference panel built from the component help output.
public sealed partial class ConfigMakerViewModel
{
    private readonly ComponentCommandHelpService _helpService = new();
    private ComponentCommandHelpDocument _helpDocument = new();
    private CancellationTokenSource _helpCancellation;
    private object _selectedCommandModule;
    private string _commandSearchText = string.Empty;
    private ConfigMakerCommandOptionViewModel _selectedCommand;
    private bool _isHelpLoading;
    private bool _hasCommandModules = true;
    private string _usageText = string.Empty;
    private string _commandArgumentText = string.Empty;
    private string _commandArgumentPlaceholder = string.Empty;
    private string _commandArgumentLabel = string.Empty;

    public ObservableCollection<ConfigMakerCommandOptionViewModel> CommandOptions { get; } = [];
    public ObservableCollection<ConfigMakerCommandModuleViewModel> CommandModules { get; } = [];
    public RelayCommand CopyCommandCommand { get; }
    public RelayCommand InsertCommandCommand { get; }

    /// <summary>The module filter; <see cref="object"/> because the combo box binds it two-way.</summary>
    public object SelectedCommandModule
    {
        get => _selectedCommandModule;
        set
        {
            if (SetProperty(ref _selectedCommandModule, value))
            {
                RebuildCommandOptions();
            }
        }
    }

    /// <summary>Set by the view when the user types in the search box.</summary>
    public string CommandSearchText
    {
        get => _commandSearchText;
        set
        {
            _commandSearchText = value ?? string.Empty;
            RebuildCommandOptions();
        }
    }

    public bool IsHelpLoading
    {
        get => _isHelpLoading;
        private set
        {
            if (SetProperty(ref _isHelpLoading, value))
            {
                OnPropertyChanged(nameof(IsCommandListEnabled));
            }
        }
    }

    public bool IsCommandListEnabled => !_isHelpLoading;

    public bool HasCommandModules
    {
        get => _hasCommandModules;
        private set => SetProperty(ref _hasCommandModules, value);
    }

    public string UsageText
    {
        get => _usageText;
        private set => SetProperty(ref _usageText, value);
    }

    public bool HasSelectedCommand => _selectedCommand != null;
    public bool CanInsertCommand => !IsEditorReadOnly && _selectedCommand != null;
    public string SelectedCommandName => _selectedCommand?.Source.DisplayName ?? string.Empty;
    public string SelectedCommandSyntax => _selectedCommand?.Source.Syntax ?? string.Empty;
    public string SelectedCommandDescription => _selectedCommand?.Description ?? string.Empty;
    public bool HasCommandArgument => !string.IsNullOrWhiteSpace(_selectedCommand?.Source.ArgumentPlaceholder);

    public string CommandArgumentText
    {
        get => _commandArgumentText;
        set => SetProperty(ref _commandArgumentText, value ?? string.Empty);
    }

    public string CommandArgumentPlaceholder
    {
        get => _commandArgumentPlaceholder;
        private set => SetProperty(ref _commandArgumentPlaceholder, value);
    }

    public string CommandArgumentLabel
    {
        get => _commandArgumentLabel;
        private set => SetProperty(ref _commandArgumentLabel, value);
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
        _selectedCommand = command;
        if (command != null)
        {
            ComponentCommandHelpOption option = command.Source;
            CommandArgumentText = string.Empty;
            CommandArgumentPlaceholder = option.ArgumentPlaceholder;
            CommandArgumentLabel = HasCommandArgument
                ? string.Format(
                    _localizer.GetLocalizedString(
                        option.IsArgumentRequired
                            ? "ConfigMakerRequiredArgumentLabel"
                            : "ConfigMakerOptionalArgumentLabel"),
                    option.ArgumentPlaceholder)
                : string.Empty;
        }
        OnPropertiesChanged(
            nameof(SelectedCommandName),
            nameof(SelectedCommandSyntax),
            nameof(SelectedCommandDescription),
            nameof(HasCommandArgument),
            nameof(HasSelectedCommand),
            nameof(CanInsertCommand));
        CopyCommandCommand.NotifyCanExecuteChanged();
        InsertCommandCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadHelpAsync(string componentId, bool forceRefresh)
    {
        _helpCancellation?.Cancel();
        _helpCancellation?.Dispose();
        _helpCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _helpCancellation.Token;

        IsHelpLoading = true;
        UsageText = _localizer.GetLocalizedString("ConfigMakerLoadingHelpMessage");
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
            UsageText = !string.IsNullOrWhiteSpace(document.Usage)
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
                IsHelpLoading = false;
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
            IsStatusOpen = false;
        }
    }

    private void RebuildCommandOptions()
    {
        string query = _commandSearchText.Trim();
        IEnumerable<ComponentCommandHelpOption> filtered = _helpDocument.Options;
        if (_selectedCommandModule is ConfigMakerCommandModuleViewModel module && !module.IsAll)
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
        ConfigMakerCommandModuleViewModel previous = _selectedCommandModule as ConfigMakerCommandModuleViewModel;
        string previousGroup = previous?.GroupName;
        bool previousWasAll = previous?.IsAll != false;
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

        HasCommandModules = namedGroups.Count > 0;
        SelectedCommandModule = previousWasAll
            ? CommandModules[0]
            : CommandModules.FirstOrDefault(item =>
                !item.IsAll && string.Equals(item.GroupName, previousGroup, StringComparison.OrdinalIgnoreCase))
                ?? CommandModules[0];
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
        if (_selectedCommand == null)
        {
            return string.Empty;
        }

        string command = ConfigMakerCommandText.BuildOptionCommand(
            _selectedCommand.Source,
            _commandArgumentText);
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
