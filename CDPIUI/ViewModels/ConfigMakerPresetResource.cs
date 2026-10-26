#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CDPIUI.ViewModels;

public sealed class ConfigMakerPresetResource : INotifyPropertyChanged
{
    private string _alias = string.Empty;
    private string _path = string.Empty;
    private ConfigMakerResourceKind _kind;
    private bool _isBuiltIn;

    public string Alias { get => _alias; set => SetField(ref _alias, value ?? string.Empty); }
    public string Path { get => _path; set => SetField(ref _path, value ?? string.Empty); }
    public ConfigMakerResourceKind Kind { get => _kind; set => SetField(ref _kind, value); }
    public bool IsBuiltIn { get => _isBuiltIn; set => SetField(ref _isBuiltIn, value); }
    public string Reference => $"preset://{Alias}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfigMakerPresetResource Clone() => new()
    {
        Alias = Alias,
        Path = Path,
        Kind = Kind,
        IsBuiltIn = IsBuiltIn,
    };

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(Alias))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reference)));
        }
    }
}
