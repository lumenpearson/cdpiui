using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace CDPIUI.Views.Store;

public sealed class ReadyKitContentItemViewModel : INotifyPropertyChanged
{
    private string _status = string.Empty;

    public string StoreId { get; set; }
    public string Name { get; set; }
    public string Developer { get; set; }

    public string Category { get; set; }
    public string Description { get; set; }

    public ImageSource ImageSource { get; set; }
    public SolidColorBrush BackgroundBrush { get; set; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
