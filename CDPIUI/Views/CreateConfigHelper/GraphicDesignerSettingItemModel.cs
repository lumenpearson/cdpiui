using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CDPIUI.Core.Basic;

namespace CDPIUI.Views.CreateConfigHelper;

public partial class GraphicDesignerSettingItemModel : INotifyPropertyChanged
{
    public string Guid { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public List<EnumModel> AvailableEnumValues { get; set; } = [];
    public bool EnableTextInput { get; set; }
    private string _value = "";
    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }

    private bool _isChecked = false;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    private ICommand _designerTextValueChangedCommand;
    public ICommand DesignerTextValueChangedCommand
    {
        get => _designerTextValueChangedCommand;
        set => SetField(ref _designerTextValueChangedCommand, value);
    }

    private ICommand _designerBoolValueToggledCommand;
    public ICommand DesignerBoolValueToggledCommand
    {
        get => _designerBoolValueToggledCommand;
        set => SetField(ref _designerBoolValueToggledCommand, value);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
