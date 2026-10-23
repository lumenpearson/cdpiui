using CDPIUI.Controls.Store.Settings;
using CDPIUI.Extensions;
using CDPIUI.Core;
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store;
using CDPIUI.Helper.LScript;

using CDPIUI.Core.Data;
using CDPIUI.Shared;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Core.Basic;
using CDPIUI.Controls.Default;
using CDPIUI.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store.Settings.Memory
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MemoryViewInstalledItemsDetailsPage : TemplatePage
    {
        private ObservableCollection<LibraryItemModel> LibraryItems = [];

        private ILocalizer localizer = Localizer.Get();
        public MemoryViewInstalledItemsDetailsPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = NavGrid;

            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;
            ItemsListView.ItemsSource = LibraryItems;

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

        private ObservableCollection<BreadcrumbBarModel> BreadcrumbBarModels = [];
        public void CreateBreadcrumbBarNavigation()
        {
            BreadcrumbBarModels.Clear();
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("Settings"),
                Tag = typeof(SettingsPage)
            });
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("MemoryUsage"),
                Tag = typeof(MemoryViewPage)
            });
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("MemoryViewInstalledItemsDetails"),
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
                LibraryItems.Clear();
                List<DatabaseStoreItem> databaseStoreItems = DatabaseHelper.Instance.GetAllInstalledItems();

                foreach (DatabaseStoreItem item in databaseStoreItems)
                {
                    if (item.Id == SharedConstants.ApplicationStoreId) continue;

                    string title = StoreHelper.Instance.GetLocalizedStoreItemName(item.Name, StoreLocalizationHelper.GetStoreLikeLocale());
                    title = title.StartsWith("slocale:") ? item.ShortName : title;

                    string category = localizer.GetLocalizedString(item.Type);
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
                        Size = await FileSystemService.GetDirectorySize(item.Directory)
                    };
                    LibraryItems.Add(libraryItemModel);
                }
                LibraryItems.Sort();
                // _libraryItems.Reverse();
                await Task.CompletedTask;
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(nameof(MemoryViewInstalledItemsDetailsPage), exception.ToString());
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
}
