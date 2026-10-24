using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store.Settings.Memory;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MemoryViewLogsDetailsPage : TemplatePage
{
    private ILocalizer _localizer = Localizer.Get();
    public MemoryViewLogsDetailsPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = NavGrid;

        BreadcrumbBar.ItemsSource = _breadcrumbBarModels;

        CreateBreadcrumbBarNavigation();

        _ = CalcSize(); // CalcSize catches and logs its own errors.
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        try
        {
            if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
            {
                PrepareToConnectedBackwardAnimate(NavGrid);
            }
        }
        // The connected animation is cosmetic; the navigation goes on without it.
        catch { }
    }

    private ObservableCollection<BreadcrumbBarModel> _breadcrumbBarModels = [];
    public void CreateBreadcrumbBarNavigation()
    {
        _breadcrumbBarModels.Clear();
        _breadcrumbBarModels.Add(new()
        {
            DisplayName = _localizer.GetLocalizedString("Settings"),
            Tag = typeof(SettingsPage)
        });
        _breadcrumbBarModels.Add(new()
        {
            DisplayName = _localizer.GetLocalizedString("MemoryUsage"),
            Tag = typeof(MemoryViewPage)
        });
        _breadcrumbBarModels.Add(new()
        {
            DisplayName = _localizer.GetLocalizedString("MemoryViewLogsDetails"),
            Tag = this.GetType()
        });

    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = (BreadcrumbBarModel)args.Item;
        Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    private void ViewAppDirButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Text = string.Format(_localizer.GetLocalizedString("ErrorHappensWhileCleanup"), "DIR_CLEANUP_UNKNOWN");
        ShellHelper.LookupDirectory(Path.Combine(CDPIUI.Core.Data.Directories.DataDirectory, "Logs"));
    }

    private async void CleanupDirButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorStackPanel.Visibility = Visibility.Collapsed;
        CleanupDirButton.IsEnabled = false;
        try
        {
            Directory.Delete(Path.Combine(CDPIUI.Core.Data.Directories.DataDirectory, "Logs"), true);
            MemoryTextBlock.Text = UnitsParser.FormatSize(0);
        }
        catch (Exception ex)
        {
            ErrorStackPanel.Visibility = Visibility.Visible;
            ErrorTextBlock.Text = string.Format(_localizer.GetLocalizedString("ErrorHappensWhileCleanup"), ErrorsHelper.Convertor.GetPrettyErrorCode("DIR_CLEANUP", ex));
            CleanupDirButton.IsEnabled = true;
        }
        await Task.CompletedTask;
    }

    private async Task CalcSize()
    {
        try
        {
            MemoryTextBlock.Text = UnitsParser.FormatSize(
                await FileSystemService.GetDirectorySize(Path.Combine(CDPIUI.Core.Data.Directories.DataDirectory, "Logs"),
                Logger.Instance));
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(MemoryViewLogsDetailsPage), exception.ToString());
        }
    }
}
