using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace CDPIUI.ViewModels.ComponentViews;

/// <summary>
/// State of one setting of the graphic config designer: which editor the setting type
/// shows, the text, the check state, the selected enum value and the selected file.
/// The values come from the dependency properties of the control; the bound properties
/// are raised on every set, because the controls also change when the user edits them.
/// </summary>
public sealed partial class ComponentSettingViewModel : ObservableObject
{
    private const string FlagType = "flag";
    private const string StringType = "string";
    private const string IntegerType = "integer";
    private const string FilePathType = "file_path";
    private const string EnumType = "enum";

    private readonly IComponentSettingView _view;

    private string _text = string.Empty;
    private bool _isChecked;
    private EnumModel _selectedEnumValue;
    private bool _isCheckBoxVisible;
    private bool _isTextBoxVisible = true;
    private bool _isToggleSwitchVisible;
    private bool _isEnumVisible;
    private bool _isFilePathVisible = true;
    private bool _isSelectFileVisible = true;
    private bool _isSelectedFileVisible = true;
    private string _selectedFileName = string.Empty;

    public ComponentSettingViewModel(IComponentSettingView view)
    {
        _view = view;
        RemoveFileCommand = new RelayCommand(RemoveFile);
    }

    public ICommand RemoveFileCommand { get; }

    /// <summary>Whether the check box (and not the toggle switch) holds the check state.</summary>
    public bool UsesCheckBox => _view.EnableTextInput || _view.Type == FilePathType;

    public string Text
    {
        get => _text;
        private set
        {
            _text = value;
            OnPropertyChanged();
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        private set
        {
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    /// <summary>The enum values of the setting; raised before the selection that depends on them.</summary>
    public List<EnumModel> EnumValues => _view.AvailableEnumValues;

    public EnumModel SelectedEnumValue
    {
        get => _selectedEnumValue;
        private set
        {
            _selectedEnumValue = value;
            OnPropertyChanged();
        }
    }

    public bool IsCheckBoxVisible
    {
        get => _isCheckBoxVisible;
        private set => SetProperty(ref _isCheckBoxVisible, value);
    }

    public bool IsTextBoxVisible
    {
        get => _isTextBoxVisible;
        private set => SetProperty(ref _isTextBoxVisible, value);
    }

    public bool IsToggleSwitchVisible
    {
        get => _isToggleSwitchVisible;
        private set => SetProperty(ref _isToggleSwitchVisible, value);
    }

    public bool IsEnumVisible
    {
        get => _isEnumVisible;
        private set => SetProperty(ref _isEnumVisible, value);
    }

    public bool IsFilePathVisible
    {
        get => _isFilePathVisible;
        private set => SetProperty(ref _isFilePathVisible, value);
    }

    public bool IsSelectFileVisible
    {
        get => _isSelectFileVisible;
        private set => SetProperty(ref _isSelectFileVisible, value);
    }

    /// <summary>Whether the buttons that view and remove the selected file are shown.</summary>
    public bool IsSelectedFileVisible
    {
        get => _isSelectedFileVisible;
        private set => SetProperty(ref _isSelectedFileVisible, value);
    }

    public string SelectedFileName
    {
        get => _selectedFileName;
        private set => SetProperty(ref _selectedFileName, value);
    }

    public static string QuotePath(string path) => $"\"{path.Replace("\"", "")}\"";

    public static string UnquotePath(string path) => path.Replace("\"", "");

    /// <summary>Runs the logic of the text value property and stores the value.</summary>
    public void SetTextValue(string value)
    {
        if (_view.Type == FilePathType && !string.IsNullOrEmpty(value))
        {
            value = QuotePath(value);
            _view.StoreTextValue(value);
            _view.NotifyValueChanged();
        }
        else
        {
            _view.StoreTextValue(value);
        }

        Text = value;
        if (_view.Type == EnumType)
            UpdateEnumSelection();
        if (_view.Type == FilePathType)
            UpdateFileButtons();
    }

    /// <summary>Shows the editor of the current setting type.</summary>
    public void ApplyType()
    {
        IsCheckBoxVisible = false;
        IsTextBoxVisible = false;
        IsToggleSwitchVisible = false;
        IsEnumVisible = false;
        IsFilePathVisible = false;

        switch (_view.Type)
        {
            case FlagType:
                IsToggleSwitchVisible = true;
                break;
            case StringType:
            case IntegerType:
                IsCheckBoxVisible = true;
                IsTextBoxVisible = true;
                break;
            case FilePathType:
                IsCheckBoxVisible = true;
                IsFilePathVisible = true;

                UpdateFileButtons();
                if (!string.IsNullOrEmpty(_view.TextValue))
                    SetTextValue(QuotePath(_view.TextValue));
                break;
            case EnumType:
                IsEnumVisible = true;
                UpdateEnumSelection();
                break;
        }
    }

    public void ShowChecked(bool value)
    {
        IsChecked = value;
    }

    /// <summary>Shows new enum values and selects the current value among them.</summary>
    public void ApplyEnumValues()
    {
        OnPropertyChanged(nameof(EnumValues));
        UpdateEnumSelection();
    }

    public void UpdateEnumSelection()
    {
        SelectedEnumValue = _view.AvailableEnumValues?.FirstOrDefault(x => x.ActualValue == _view.TextValue);
    }

    /// <summary>Called by the view when the user selects an enum value.</summary>
    public void SelectEnumValue(EnumModel item)
    {
        string value = item?.ActualValue ?? _view.TextValue;
        if (value != _view.TextValue)
        {
            SetTextValue(value);
            _view.NotifyValueChanged();
        }
    }

    private void UpdateFileButtons()
    {
        string textValue = _view.TextValue;
        bool hasFile = !string.IsNullOrEmpty(textValue);

        IsSelectFileVisible = !hasFile;
        IsSelectedFileVisible = hasFile;
        if (hasFile)
            SelectedFileName = Path.GetFileName(UnquotePath(textValue));
    }

    private void RemoveFile()
    {
        SetTextValue(string.Empty);
        _view.NotifyValueChanged();
    }
}
