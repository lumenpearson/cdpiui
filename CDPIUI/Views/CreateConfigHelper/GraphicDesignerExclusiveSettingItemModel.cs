using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CDPIUI.Views.CreateConfigHelper;

public partial class GraphicDesignerExclusiveSettingItemModel : INotifyPropertyChanged
{
    public string Guid { get; set; }
    public string DisplayName { get; set; }

    private string _selectedItemGuid = "";
    public string SelectedItemGuid
    {
        get => _selectedItemGuid;
        set => SetField(ref _selectedItemGuid, value);
    }
    public ObservableCollection<GraphicDesignerSettingItemModel> Items { get; set; }

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

    private ICommand _designerSelectedGuidChangedCommand;
    public ICommand DesignerSelectedGuidChangedCommand
    {
        get => _designerSelectedGuidChangedCommand;
        set => SetField(ref _designerSelectedGuidChangedCommand, value);
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
