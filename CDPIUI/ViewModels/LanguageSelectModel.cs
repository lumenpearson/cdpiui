using System;
using System.ComponentModel;

namespace CDPIUI.ViewModels;

public class LanguageSelectModel : INotifyPropertyChanged
{
    private string _displayName = null!;

    public string Id { get; set; } = null!;
    public string DisplayId { get => (string)new Converters.StringToUpperConverter().Convert(Id, null!, null!, null!); }
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal))
                return;

            _displayName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
