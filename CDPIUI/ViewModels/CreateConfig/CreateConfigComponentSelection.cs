#nullable enable

using System;

namespace CDPIUI.ViewModels.CreateConfig;

// Owns component/name choice, related view flags and the original ordered UI effects.
internal sealed class CreateConfigComponentSelection : ObservableObject
{
    private readonly Func<string, bool> _supported;
    private readonly Action _selectDesigner;
    private readonly Action _selectText;
    private readonly Action<bool> _handleSelection;
    private readonly Action _dirty;
    private readonly Action _audit;
    private readonly Func<string?> _editorText;
    private ConfigComponentOption? _selectedComponent;
    private string _displayName = string.Empty;
    private bool _isAdditionalSettingsVisible;
    private bool _isNotReadyErrorVisible = true;
    private bool _isTabBarVisible = true;

    public CreateConfigComponentSelection(Func<string, bool> supported,
        Action selectDesigner, Action selectText, Action<bool> handleSelection,
        Action dirty, Action audit, Func<string?> editorText)
    {
        _supported = supported;
        _selectDesigner = selectDesigner;
        _selectText = selectText;
        _handleSelection = handleSelection;
        _dirty = dirty;
        _audit = audit;
        _editorText = editorText;
    }

    public ConfigComponentOption? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (!SetProperty(ref _selectedComponent, value))
                return;
            CheckViewType();
            _dirty();
            _audit();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value ?? string.Empty))
                return;
            _dirty();
            _audit();
        }
    }

    public bool IsAdditionalSettingsVisible
    {
        get => _isAdditionalSettingsVisible;
        set => SetProperty(ref _isAdditionalSettingsVisible, value);
    }
    public bool IsNotReadyErrorVisible
    {
        get => _isNotReadyErrorVisible;
        set => SetProperty(ref _isNotReadyErrorVisible, value);
    }
    public bool IsTabBarVisible
    {
        get => _isTabBarVisible;
        set => SetProperty(ref _isTabBarVisible, value);
    }
    public bool IsDesignerSupported => _supported(SelectedComponent?.Id ?? "");
    public bool IsSaveAvailable => SelectedComponent != null &&
        !string.IsNullOrEmpty(DisplayName) && !string.IsNullOrEmpty(_editorText());

    public void CheckViewType()
    {
        if (SelectedComponent == null)
        {
            IsAdditionalSettingsVisible = false;
            IsNotReadyErrorVisible = true;
            return;
        }
        IsAdditionalSettingsVisible = true;
        IsNotReadyErrorVisible = false;
        if (IsDesignerSupported)
        {
            _selectDesigner();
            IsTabBarVisible = true;
            _handleSelection(false);
        }
        else
        {
            _handleSelection(true);
            _selectText();
            IsTabBarVisible = false;
        }
    }
}
