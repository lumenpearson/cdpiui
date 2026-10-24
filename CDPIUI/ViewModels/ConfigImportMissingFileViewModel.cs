using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CDPIUI.Views.ConfigImportUtil;
using WinUI3Localizer;

namespace CDPIUI.ViewModels;

public sealed class ConfigImportMissingFileViewModel : INotifyPropertyChanged
{
    private readonly ConfigImportPresetViewModel _owner;
    private readonly ILocalizer _localizer = Localizer.Get();
    private bool _isResolved;
    private string? _replacementPath;
    private string _resolutionText;

    public ConfigImportMissingFileViewModel(
        ConfigImportPresetViewModel owner,
        string missingPath,
        string? suggestedPath,
        bool suggestEmptyFile)
    {
        _owner = owner;
        MissingPath = Path.GetFullPath(missingPath);
        SuggestedPath = string.IsNullOrWhiteSpace(suggestedPath) ? null : Path.GetFullPath(suggestedPath);
        SuggestEmptyFile = suggestEmptyFile;
        _resolutionText = _localizer.GetLocalizedString("ConfigImportMissingFileUnresolved");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ConfigName => _owner.Name;

    public string MissingPath { get; }

    public string? SuggestedPath { get; }

    public bool SuggestEmptyFile { get; }

    public string SuggestionText => SuggestEmptyFile
        ? _localizer.GetLocalizedString("ConfigImportMissingFileEmptySuggestion")
        : SuggestedPath ?? string.Empty;

    public bool HasSuggestion => SuggestEmptyFile || !string.IsNullOrWhiteSpace(SuggestedPath);

    public bool IsResolved => _isResolved;

    public string ResolutionText => _resolutionText;

    public void UseSuggestion()
    {
        if (SuggestEmptyFile)
        {
            UseEmptyFile();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SuggestedPath) && File.Exists(SuggestedPath))
            UseReplacement(SuggestedPath);
    }

    public void UseReplacement(string path)
    {
        _replacementPath = Path.GetFullPath(path);
        _resolutionText = string.Format(
            _localizer.GetLocalizedString("ConfigImportMissingFileReplacementSelected"),
            _replacementPath);
        CompleteResolution();
    }

    public void UseEmptyFile()
    {
        _replacementPath = null;
        _resolutionText = _localizer.GetLocalizedString("ConfigImportMissingFileEmptySelected");
        CompleteResolution();
    }

    private void CompleteResolution()
    {
        _isResolved = true;
        _owner.SetMissingFileResolution(MissingPath, _replacementPath);
        Notify(nameof(IsResolved));
        Notify(nameof(ResolutionText));
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
