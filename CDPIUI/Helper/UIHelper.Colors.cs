using System.Globalization;
using CDPIUI.Core.Basic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CDPIUI.Helper;

public static partial class UIHelper
{
    /// <summary>
    /// Parses #RRGGBB or #AARRGGBB. A color from a store manifest is not trusted to be valid.
    /// </summary>
    static public bool TryParseHexColor(string hexColor, out Color color)
    {
        color = Color.FromArgb(0, 0, 0, 0);
        string value = hexColor?.Trim().TrimStart('#');
        if (value == null || (value.Length != 6 && value.Length != 8) ||
            !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint number))
        {
            return false;
        }
        byte a = value.Length == 8 ? (byte)(number >> 24) : (byte)255;
        color = Color.FromArgb(a, (byte)(number >> 16), (byte)(number >> 8), (byte)number);
        return true;
    }

    static public Color HexToColorConverter(string hexColor)
    {
        if (!TryParseHexColor(hexColor, out Color color))
            Logger.Instance.CreateWarningLog(nameof(UIHelper), $"\"{hexColor}\" is not a color");
        return color;
    }

    static public SolidColorBrush HexToSolidColorBrushConverter(string hexColor)
    {
        Color color;
        if (string.IsNullOrEmpty(hexColor))
        {
            color = Color.FromArgb(0, 0, 0, 0);
        }
        else
        {
            color = HexToColorConverter(hexColor);
        }
        SolidColorBrush br = new SolidColorBrush(color);

        return br;
    }
}
