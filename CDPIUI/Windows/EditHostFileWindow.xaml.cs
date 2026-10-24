using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.Default;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditHostFileWindow : TemplateWindow
{
    private ILocalizer _localizer = Localizer.Get();
    public EditHostFileWindow()
    {
        InitializeComponent();

        WindowTitle = _localizer.GetLocalizedString("EditHostsFileWindowTitle");

        DisableResizeFeature();

        IconUri = @"Assets/Icons/EditHostsFile.ico";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        MainFrame = ContentFrame;

        ContentFrame.Navigate(typeof(Views.EditHostsFile.MainPage));

    }
}
