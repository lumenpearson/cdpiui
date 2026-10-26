using System.Diagnostics;
using CDPIUI.Helper.WindowHelper;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace CDPIUI;

public partial class App
{
    private ElementTheme GetThemeFromString(string theme)
    {
        if (theme == "Dark")
        {
            return ElementTheme.Dark;
        }
        else if (theme == "Light")
        {
            return ElementTheme.Light;
        }
        else
        {
            return ElementTheme.Default;
        }
    }

    public ElementTheme GetCurrentTheme()
    {
        return CurrentTheme;
    }

    public void UpdateThemeForWindow(Window window, ElementTheme theme)
    {
        if (window.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
            if (theme == ElementTheme.Dark)
            {
                TitleBarHelper.SetCaptionButtonColors(window, Colors.White);
            }
            else if (theme == ElementTheme.Light)
            {
                TitleBarHelper.SetCaptionButtonColors(window, Colors.Black);
            }
            else
            {
                TitleBarHelper.ApplySystemThemeToCaptionButtons(window);
            }
        }
    }

    public void UpdateThemeForAllWindows(ElementTheme theme)
    {
        CurrentTheme = theme;


        foreach (var window in OpenWindows)
        {
            try
            {
                UpdateThemeForWindow(window, theme);
            }
            catch
            {
                Debug.WriteLine("Something went wrong");
            }
        }
    }
}
