#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CDPIUI.Controls.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

// Owns the reference panel's filtering and selection; async help loading and editor actions stay outside.
internal sealed class ConfigMakerCommandHelpPanel : INotifyPropertyChanged
{
    private readonly Func<string, string> _localize;
    private readonly Func<bool> _isEditorReadOnly;
    private object? _selectedCommandModule;
    private string _commandSearchText = string.Empty;
    private ConfigMakerCommandOptionViewModel? _selectedCommand;
    private bool _hasCommandModules = true;
    private string _commandArgumentText = string.Empty;
    private string _commandArgumentPlaceholder = string.Empty;
    private string _commandArgumentLabel = string.Empty;

    public ConfigMakerCommandHelpPanel(Func<string, string> localize, Func<bool> isEditorReadOnly)
    {
        _localize = localize;
        _isEditorReadOnly = isEditorReadOnly;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ComponentCommandHelpDocument Document { get; set; } = new();
    public ObservableCollection<ConfigMakerCommandOptionViewModel> CommandOptions { get; } = [];
    public ObservableCollection<ConfigMakerCommandModuleViewModel> CommandModules { get; } = [];

    public object? SelectedCommandModule
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

    public bool HasCommandModules
    {
        get => _hasCommandModules;
        private set => SetProperty(ref _hasCommandModules, value);
    }

    public bool HasSelectedCommand => _selectedCommand != null;
    public bool CanInsertCommand => !_isEditorReadOnly() && _selectedCommand != null;
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

    public void SelectCommand(ConfigMakerCommandOptionViewModel? command)
    {
        _selectedCommand = command;
        if (command != null)
        {
            ComponentCommandHelpOption option = command.Source;
            CommandArgumentText = string.Empty;
            CommandArgumentPlaceholder = option.ArgumentPlaceholder;
            CommandArgumentLabel = HasCommandArgument
                ? string.Format(
                    _localize(
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
    }

    public void RebuildCommandOptions()
    {
        string query = _commandSearchText.Trim();
        IEnumerable<ComponentCommandHelpOption> filtered = Document.Options;
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
                    ? _localize("ConfigMakerNoDescriptionMessage")
                    : option.Description,
            });
        }
    }

    public void RebuildCommandModules()
    {
        ConfigMakerCommandModuleViewModel? previous = _selectedCommandModule as ConfigMakerCommandModuleViewModel;
        string? previousGroup = previous?.GroupName;
        bool previousWasAll = previous?.IsAll != false;
        List<string> namedGroups = Document.Options
            .Select(option => option.GroupName)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CommandModules.Clear();
        CommandModules.Add(new ConfigMakerCommandModuleViewModel
        {
            DisplayName = _localize("ConfigMakerAllModules"),
            IsAll = true,
        });

        if (namedGroups.Count > 0 && Document.Options.Any(option => string.IsNullOrWhiteSpace(option.GroupName)))
        {
            CommandModules.Add(new ConfigMakerCommandModuleViewModel
            {
                DisplayName = _localize("ConfigMakerGeneralModule"),
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

    public string? BuildSelectedCommand() => _selectedCommand == null
        ? string.Empty
        : ConfigMakerCommandText.BuildOptionCommand(_selectedCommand.Source, _commandArgumentText);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void OnPropertiesChanged(params string[] propertyNames)
    {
        foreach (string name in propertyNames)
        {
            OnPropertyChanged(name);
        }
    }
}
