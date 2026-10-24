using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
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

namespace CDPIUI.Views.Store.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public class MemoryDiskViewModel
{
    public string DiskLetter { get; set; }
}
public sealed partial class MemoryViewPage : TemplatePage
{
    private ObservableCollection<MemoryDiskViewModel> _memoryDisks = [];

    private ILocalizer _localizer = Localizer.Get();
    public MemoryViewPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = NavGrid;
        IsBackwardAnimationToPageAvailable = true;
        ElementToAnimateBackwardConnectedAnimation = NavGrid;

        BreadcrumbBar.ItemsSource = _breadcrumbBarModels;
        DiskInfoListView.ItemsSource = _memoryDisks;

        CreateBreadcrumbBarNavigation();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        CreateDiskView();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        try
        {
            if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
            {
                PrepareToConnectedForwardAnimate(NavGrid);
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
            Tag = this.GetType()
        });
    }

    public void CreateDiskView()
    {
        _memoryDisks.Clear();

        string appDir = CDPIUI.Core.Data.Directories.CurrentDirectory;
        string dataDir = CDPIUI.Core.Data.Directories.DataDirectory;

        DriveInfo appDirDriveInfo = new(appDir);
        DriveInfo dataDirDriveInfo = new(dataDir);

        if (appDirDriveInfo.Name == dataDirDriveInfo.Name)
            _memoryDisks.Add(new() { DiskLetter = appDirDriveInfo.Name });
        else
        {
            _memoryDisks.Add(new() { DiskLetter = appDirDriveInfo.Name });
            _memoryDisks.Add(new() { DiskLetter = dataDirDriveInfo.Name });
        }
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = (BreadcrumbBarModel)args.Item;
        Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }
}
