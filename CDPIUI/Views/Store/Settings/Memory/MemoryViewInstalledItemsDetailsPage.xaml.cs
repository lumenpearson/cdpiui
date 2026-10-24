using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Store.Settings;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store.Settings.Memory;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MemoryViewInstalledItemsDetailsPage : TemplatePage
{
    private ObservableCollection<LibraryItemModel> _libraryItems = [];

    private ILocalizer _localizer = Localizer.Get();
    public MemoryViewInstalledItemsDetailsPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = NavGrid;

        BreadcrumbBar.ItemsSource = _breadcrumbBarModels;
        ItemsListView.ItemsSource = _libraryItems;

        CreateBreadcrumbBarNavigation();
        StoreHelper.Instance.ItemRemoved += StoreHelper_ItemRemoved;
        StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;

        _ = CalcSize(); // CalcSize catches and logs its own errors.
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _ = LoadItems(); // LoadItems catches and logs its own errors.
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

        StoreHelper.Instance.ItemRemoved -= StoreHelper_ItemRemoved;
        StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;


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
            DisplayName = _localizer.GetLocalizedString("MemoryViewInstalledItemsDetails"),
            Tag = this.GetType()
        });

    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = (BreadcrumbBarModel)args.Item;
        Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
    }

    public async Task LoadItems()
    {
        try
        {
            _libraryItems.Clear();
            List<DatabaseStoreItem> databaseStoreItems = DatabaseHelper.Instance.GetAllInstalledItems();
            List<LibraryItemModel> models = [];
            List<Task<long>> sizeTasks = [];
            // Folder sizes are read a few at a time instead of one after another.
            using SemaphoreSlim sizeGate = new(4);

            foreach (DatabaseStoreItem item in databaseStoreItems)
            {
                if (item.Id == SharedConstants.ApplicationStoreId)
                    continue;

                string title = StoreHelper.Instance.GetLocalizedStoreItemName(item.Name, StoreLocalizationHelper.GetStoreLikeLocale());
                title = title.StartsWith("slocale:") ? item.ShortName : title;

                string category = _localizer.GetLocalizedString(item.Type);
                category = string.IsNullOrEmpty(category) ? item.Type : category;

                string eImageSource = LScriptLangHelper.ExecuteScript(item.IconPath);

                BitmapImage image = new BitmapImage(UIHelper.GetUriFromString(eImageSource));

                SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(item.BackgroundColor);

                LibraryItemModel libraryItemModel = new()
                {
                    StoreId = item.Id,
                    Title = title,
                    Developer = item.Developer,
                    Category = category,
                    ImageSource = image,
                    CardBackgroundBrush = solidColorBrush,
                };
                models.Add(libraryItemModel);
                sizeTasks.Add(GetDirectorySizeAsync(item.Directory, sizeGate));
            }

            long[] sizes = await Task.WhenAll(sizeTasks);
            // Another reload may have filled the list while the sizes were read.
            _libraryItems.Clear();
            for (int i = 0; i < models.Count; i++)
            {
                models[i].Size = sizes[i];
                _libraryItems.Add(models[i]);
            }
            _libraryItems.Sort();
            // _libraryItems.Reverse();
            await Task.CompletedTask;
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(MemoryViewInstalledItemsDetailsPage), exception.ToString());
        }
    }



    private static async Task<long> GetDirectorySizeAsync(string directory, SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            return await FileSystemService.GetDirectorySize(directory);
        }
        finally
        {
            gate.Release();
        }
    }

    private void StoreHelper_ItemActionsStopped(string obj)
    {
        // LoadItems and CalcSize catch and log their own errors.
        _ = LoadItems();
        _ = CalcSize();
    }

    private void StoreHelper_ItemRemoved(string obj)
    {
        // LoadItems and CalcSize catch and log their own errors.
        _ = LoadItems();
        _ = CalcSize();
    }

    private async Task CalcSize()
    {
        try
        {
            MemoryTextBlock.Text = UnitsParser.FormatSize(await FileSystemService.GetDirectorySize(Directories.StoreItemsDirectory, Logger.Instance));
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(MemoryViewInstalledItemsDetailsPage), exception.ToString());
        }
    }


}
