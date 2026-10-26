#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CDPIUI.ViewModels;

public sealed class ConfigMakerVariableDefinition : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private ConfigMakerVariableKind _kind;
    private ConfigMakerVariableStorageKind _storageKind;
    private string _value = string.Empty;
    private string _description = string.Empty;
    private string _onValue = string.Empty;
    private string _offValue = string.Empty;
    private string _internalParameterName = string.Empty;
    private bool _isSwitchEnabled;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ObservableCollection<string> Values { get; } = [];

    public string Name { get => _name; set => SetField(ref _name, value ?? string.Empty); }
    public ConfigMakerVariableKind Kind { get => _kind; set => SetField(ref _kind, value); }
    public ConfigMakerVariableStorageKind StorageKind { get => _storageKind; set => SetField(ref _storageKind, value); }
    public string Value { get => _value; set => SetField(ref _value, value ?? string.Empty); }
    public string Description { get => _description; set => SetField(ref _description, value ?? string.Empty); }
    public string OnValue { get => _onValue; set => SetField(ref _onValue, value ?? string.Empty); }
    public string OffValue { get => _offValue; set => SetField(ref _offValue, value ?? string.Empty); }
    public string InternalParameterName { get => _internalParameterName; set => SetField(ref _internalParameterName, value ?? string.Empty); }
    public bool IsSwitchEnabled { get => _isSwitchEnabled; set => SetField(ref _isSwitchEnabled, value); }

    public string Reference => $"%{Name}%";
    public string DisplayValue => Kind == ConfigMakerVariableKind.Switch
        ? IsSwitchEnabled ? OnValue : OffValue
        : Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfigMakerVariableDefinition Clone()
    {
        ConfigMakerVariableDefinition result = new()
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            StorageKind = StorageKind,
            Value = Value,
            Description = Description,
            OnValue = OnValue,
            OffValue = OffValue,
            InternalParameterName = InternalParameterName,
            IsSwitchEnabled = IsSwitchEnabled,
        };
        foreach (string value in Values)
        {
            result.Values.Add(value);
        }
        return result;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(Name))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reference)));
        }
        if (propertyName is nameof(Value) or nameof(OnValue) or nameof(OffValue) or
            nameof(IsSwitchEnabled) or nameof(Kind))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayValue)));
        }
    }
}
