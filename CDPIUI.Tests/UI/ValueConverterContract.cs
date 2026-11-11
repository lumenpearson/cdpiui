// The linked model uses this framework interface only through its unchanged DisplayId
// converter. This headless contract is not evidence of WinUI rendering or binding;
// those behaviors are checked separately in the native installed-product acceptance.
namespace Microsoft.UI.Xaml.Data;

public interface IValueConverter
{
    object Convert(object value, Type targetType, object parameter, string language);
    object ConvertBack(object value, Type targetType, object parameter, string language);
}
