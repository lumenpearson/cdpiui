using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUI3Localizer;

namespace CDPIUI.Views.ConfigImportUtil;

public sealed class ConfigImportPresetViewModel : INotifyPropertyChanged
{
    private readonly ILocalizer _localizer = Localizer.Get();
    private readonly bool _targetWasDetected;
    private string name;
    private DatabaseStoreItem? _selectedComponent;
    private bool _isSaving;
    private bool _isSaved;
    private bool _isTesting;
    private string? _operationError;
    private readonly Dictionary<string, ConfigImportMissingFileResolution> _missingFileResolutions =
        new(StringComparer.OrdinalIgnoreCase);

    public ConfigImportPresetViewModel(
        ConfigImportResult result,
        ObservableCollection<DatabaseStoreItem> components,
        DatabaseStoreItem? initialComponent,
        bool targetWasDetected)
    {
        Result = result;
        Components = components;
        _targetWasDetected = targetWasDetected;
        name = result.SharedPackage?.Manifest.Name ?? Path.GetFileNameWithoutExtension(result.SourcePath);
        _selectedComponent = initialComponent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfigImportResult Result { get; }

    public ObservableCollection<DatabaseStoreItem> Components { get; }

    public string SourcePath => Result.SourcePath;

    public string FileName => Path.GetFileName(Result.SourcePath);

    // Shown as written in the preset, without expanding variables or running scripts,
    // so the user sees what the component will be started with before testing or saving it.
    public string Arguments => Result.Config?.startup_string ?? string.Empty;

    public string ArgumentsHeader => _localizer.GetLocalizedString("ConfigImportArguments");

    public Visibility ArgumentsVisibility =>
        string.IsNullOrWhiteSpace(Arguments) ? Visibility.Collapsed : Visibility.Visible;

    public IReadOnlyCollection<ConfigImportMissingFileResolution> MissingFileResolutions =>
        _missingFileResolutions.Values;

    public string Name
    {
        get => name;
        set
        {
            if (name == value)
                return;
            name = value;
            _isSaved = false;
            Notify();
            NotifyActionState();
        }
    }

    public DatabaseStoreItem? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (ReferenceEquals(_selectedComponent, value))
                return;
            _selectedComponent = value;
            _isSaved = false;
            Notify();
            Notify(nameof(IsUnsuitable));
            Notify(nameof(SuitabilityMessage));
            NotifyActionState();
        }
    }

    public bool IsUnsuitable =>
        Result.IsSuccessful &&
        _targetWasDetected &&
        _selectedComponent != null &&
        !ExecutablesEqual(Result.Target.Executable, _selectedComponent.Executable);

    public string SuitabilityMessage => IsUnsuitable
        ? _localizer.GetLocalizedString("ConfigImportComponentMismatchMessage")
        : string.Empty;

    public string? OperationError => _operationError;

    public bool HasDetails => Result.Issues.Count > 0 || !string.IsNullOrWhiteSpace(_operationError);

    public Visibility DetailsVisibility => HasDetails ? Visibility.Visible : Visibility.Collapsed;

    public string StatusText => Result.IsSuccessful && string.IsNullOrWhiteSpace(_operationError)
        ? Result.Issues.Count > 0
            ? _localizer.GetLocalizedString("ConfigImportItemWarningStatus")
            : _localizer.GetLocalizedString("ConfigImportItemSuccessStatus")
        : _localizer.GetLocalizedString("ConfigImportItemFailedStatus");

    public string StatusGlyph => Result.IsSuccessful && string.IsNullOrWhiteSpace(_operationError)
        ? Result.Issues.Count > 0 ? "\uE7BA" : "\uE930"
        : "\uEA39";

    public Brush StatusForeground => new SolidColorBrush(Result.IsSuccessful && string.IsNullOrWhiteSpace(_operationError)
        ? Result.Issues.Count > 0 ? ColorHelper.FromArgb(255, 157, 93, 0) : ColorHelper.FromArgb(255, 16, 124, 16)
        : ColorHelper.FromArgb(255, 196, 43, 28));

    public Brush StatusBackground => new SolidColorBrush(Result.IsSuccessful && string.IsNullOrWhiteSpace(_operationError)
        ? Result.Issues.Count > 0 ? ColorHelper.FromArgb(28, 255, 185, 0) : ColorHelper.FromArgb(28, 16, 124, 16)
        : ColorHelper.FromArgb(28, 196, 43, 28));

    public bool CanSave => Result.IsSuccessful &&
                           _selectedComponent != null &&
                           !string.IsNullOrWhiteSpace(name) &&
                           !_isSaving &&
                           !_isSaved;

    public bool CanTest => Result.IsSuccessful && _selectedComponent != null && !_isSaving;

    public string SaveButtonText => _isSaved
        ? _localizer.GetLocalizedString("ConfigImportSavedButtonText")
        : _localizer.GetLocalizedString("ConfigImportSaveButtonText");

    public string TestButtonText => _isTesting
        ? _localizer.GetLocalizedString("StopTest")
        : _localizer.GetLocalizedString("ConfigImportTestButtonText");

    public string TestButtonGlyph => _isTesting
        ? (SharedUtils.IsOsSupportedNewGlyph() ? "\uF8AE" : "\uE769")
        : "\uF5B0";

    public void SetSaving(bool value)
    {
        _isSaving = value;
        NotifyActionState();
    }

    public void SetSaved()
    {
        _isSaving = false;
        _isSaved = true;
        _operationError = null;
        NotifyStatus();
        NotifyActionState();
    }

    public void SetOperationError(string error)
    {
        _isSaving = false;
        _operationError = error;
        Notify(nameof(OperationError));
        NotifyStatus();
        NotifyActionState();
    }

    public void SetTesting(bool value)
    {
        _isTesting = value;
        Notify(nameof(TestButtonText));
        Notify(nameof(TestButtonGlyph));
        Notify(nameof(CanTest));
    }

    public void SetMissingFileResolution(string missingPath, string? replacementPath)
    {
        string fullMissingPath = Path.GetFullPath(missingPath);
        _missingFileResolutions[fullMissingPath] = new ConfigImportMissingFileResolution(
            fullMissingPath,
            string.IsNullOrWhiteSpace(replacementPath) ? null : Path.GetFullPath(replacementPath));
    }

    private void NotifyActionState()
    {
        Notify(nameof(CanSave));
        Notify(nameof(CanTest));
        Notify(nameof(SaveButtonText));
    }

    private void NotifyStatus()
    {
        Notify(nameof(HasDetails));
        Notify(nameof(DetailsVisibility));
        Notify(nameof(StatusText));
        Notify(nameof(StatusGlyph));
        Notify(nameof(StatusForeground));
        Notify(nameof(StatusBackground));
    }

    private static bool ExecutablesEqual(string? left, string? right) =>
        NormalizeExecutable(left).Equals(NormalizeExecutable(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeExecutable(string? executable)
    {
        string name = Path.GetFileName((executable ?? string.Empty).Trim().Trim('"'));
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
