using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core;
using CDPIUI.Core.Features;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Repository;
using CDPIUI.Messages;
using CDPIUI.ViewModels;
using CDPIUI.Views.Store.Settings;
using CDPIUI.Views.Store.Settings.Memory;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;
using ImageSource = Microsoft.UI.Xaml.Media.ImageSource;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public class VersionControlModel
{
    public string DisplayName { get; set; }
    public ImageSource DisplayImage { get; set; }
    public SupportedVersionControls Id { get; set; }
}

public sealed partial class SettingsPage : TemplatePage
{
    public static readonly List<Type> MemoryNavigationSupportedPages = [
        typeof(SettingsPage),
        typeof(MemoryViewPage),
        typeof(MemoryViewApplicationFilesDetailsPage),
        typeof(MemoryViewInstalledItemsDetailsPage),
        typeof(MemoryViewLogsDetailsPage),
        typeof(MemoryViewSettingsDetailsPage),
        typeof(MemoryViewStoreCachePage),
        typeof(MemoryViewConditionalLaunchDetailsPage),
        typeof(MemoryViewPreviousVersionsPage)];

    private ILocalizer _localizer = Localizer.Get();

    private readonly ObservableCollection<VersionControlModel> _versionControlModels = [];
    public SettingsPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = NavGrid;
        IsBackwardAnimationToPageAvailable = true;
        ElementToAnimateBackwardConnectedAnimation = NavGrid;

        VersionControlTypeComboBox.ItemsSource = _versionControlModels;
        LoadVersionControlInfo();

        BreadcrumbBar.ItemsSource = _breadcrumbBarModels;
        CreateBreadcrumbBarNavigation();
    }

    private ObservableCollection<BreadcrumbBarModel> _breadcrumbBarModels = [];
    public void CreateBreadcrumbBarNavigation()
    {
        _breadcrumbBarModels.Clear();
        _breadcrumbBarModels.Add(new()
        {
            DisplayName = _localizer.GetLocalizedString("Settings"),
            Tag = this.GetType()
        });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        VersionControlTypeComboBox.SelectionChanged -= VersionControlTypeComboBox_SelectionChanged;
    }

    private void LoadVersionControlInfo()
    {
        _versionControlModels.Clear();

        BitmapImage githubImage = new BitmapImage(new Uri("ms-appx:///Assets/Icons/github.png"));
        _versionControlModels.Add(new VersionControlModel
        {
            DisplayImage = githubImage,
            DisplayName = _localizer.GetLocalizedString("VersionControlGitHub"),
            Id = SupportedVersionControls.GitHub
        });
        BitmapImage gitlabImage = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Gitlab.png"));
        _versionControlModels.Add(new VersionControlModel
        {
            DisplayImage = gitlabImage,
            DisplayName = _localizer.GetLocalizedString("VersionControlGitLab"),
            Id = SupportedVersionControls.GitLab
        });

        VersionControlTypeComboBox.SelectedItem = _versionControlModels.FirstOrDefault(x => x.Id.ToString() == SettingsManager.Instance.GetValueOrDefault("STORE", "versionControlType", defaultValue: "GitHub"));
        StoryProxySettingCard.Visibility = ((VersionControlModel)VersionControlTypeComboBox.SelectedItem).Id == SupportedVersionControls.GitHub ? Visibility.Visible : Visibility.Collapsed;

        VersionControlTypeComboBox.SelectionChanged += VersionControlTypeComboBox_SelectionChanged;
    }

    private async void VersionControlTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        InfoStackPanel.Visibility = Visibility.Visible;
        SettingsManager.Instance.SetValue("STORE", "versionControlType", ((VersionControlModel)VersionControlTypeComboBox.SelectedItem).Id.ToString());
        StoryProxySettingCard.Visibility = ((VersionControlModel)VersionControlTypeComboBox.SelectedItem).Id == SupportedVersionControls.GitHub ? Visibility.Visible : Visibility.Collapsed;

        StoreHelper.ClearRepoCache();
        await StoreHelper.Instance.LoadAllStoreDatabase(forseSync: true, versionControl: ((VersionControlModel)VersionControlTypeComboBox.SelectedItem).Id);
    }

    private async void ManualItemInstallation_Click(object sender, RoutedEventArgs e)
    {
        LocalItemInstallingWarningContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowQueuedAsync();

        if (result == ContentDialogResult.Secondary)
        {
            string filePath;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = _localizer.GetLocalizedString("SelectLocalPackToInstall");
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                openFileDialog.Filter = $"{_localizer.GetLocalizedString("SignedPack")} (*.cdpisignedpack)|*.cdpisignedpack|" +
                    $"{_localizer.GetLocalizedString("ConfigPack")} (*.cdpiconfigpack)|*.cdpiconfigpack|" +
                    $"{_localizer.GetLocalizedString("Patch")} (*.cdpipatch)|*.cdpipatch|" +
                    $"{_localizer.GetLocalizedString("AllSupported")} (*.cdpiconfigpack, *.cdpipatch, *.cdpisignedpack)|*.cdpiconfigpack;*.cdpipatch;*.cdpisignedpack";
                openFileDialog.FilterIndex = 4;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            if (Path.GetExtension(filePath).Equals(
                ".cdpipatch",
                StringComparison.OrdinalIgnoreCase))
            {
                var updateDialog = await ((App)Application.Current)
                    .SafeCreateNewWindow<ApplicationUpdateFileDialogWindow>();
                updateDialog.SetUpdateFilePath(filePath);
            }
            else
            {
                var window = await ((App)Application.Current).SafeCreateNewWindow<StoreLocalItemInstallingDialog>();
                // Not awaited: the import runs while the dialog is open. SetPackFilePath logs its own errors.
                _ = window.SetPackFilePath(filePath);
            }
        }
    }

    private async void MemoryManagentSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();

        PrepareToConnectedForwardAnimate(NavGrid);

        window.NavigateSubPage(typeof(MemoryViewPage), new NameValueCollection(), new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }
}
