using System.Collections.Specialized;
using CDPIUI.Default;
using CDPIUI.Views.ConfigImportUtil;
using Microsoft.UI.Xaml.Media.Animation;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ConfigImportUtilWindow : TemplateWindow
{
    private string _targetStoreId = string.Empty;
    public string TargetStoreId
    {
        get => _targetStoreId;
        set
        {
            _targetStoreId = value ?? string.Empty;
            ContentFrame.Navigate(
                typeof(MainPage),
                new NameValueCollection { { "componentId", _targetStoreId } },
                new SuppressNavigationTransitionInfo());
        }
    }

    public void ImportFiles(string[] paths, string componentId)
    {
        TargetStoreId = componentId;
        ((MainPage)ContentFrame.Content).QueueDroppedFiles(paths);
    }

    public ConfigImportUtilWindow()
    {
        InitializeComponent();

        ILocalizer localizer = Localizer.Get();
        WindowTitle = localizer.GetLocalizedString("ConfigImportUtilWindowTitle");
        IconUri = @"Assets/Icons/Import.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        DisableResizeFeature();

        MainFrame = ContentFrame;

        ContentFrame.Navigate(
            typeof(MainPage),
            new NameValueCollection { { "componentId", _targetStoreId } },
            new SuppressNavigationTransitionInfo());
    }
}
