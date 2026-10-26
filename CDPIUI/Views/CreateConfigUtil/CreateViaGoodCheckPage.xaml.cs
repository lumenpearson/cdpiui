using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Controls.Default;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels.CreateConfig;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI.Views.CreateConfigUtil;

public sealed partial class CreateViaGoodCheck : TemplatePage, ICreateViaGoodCheckView
{
    private readonly ILocalizer _localizer = Localizer.Get();

    public CreateViaGoodCheck()
    {
        ViewModel = new CreateViaGoodCheckViewModel(this);
        InitializeComponent();

        ViewModel.SiteListTemplates.CollectionChanged += SiteListTemplates_CollectionChanged;
        ViewModel.SelectedSiteLists.CollectionChanged += SelectedSiteLists_CollectionChanged;
    }

    public CreateViaGoodCheckViewModel ViewModel { get; }

    public void HideAllPanels()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        LetsBeginStackPanel.Visibility = Visibility.Collapsed;
        GoodCheckSettingsStackPanel.Visibility = Visibility.Collapsed;
    }

    public bool SwitchPanel(GoodCheckWizardState oldState, GoodCheckWizardState newState, bool slideToLeft)
    {
        var oldPanel = GetPanelForState(oldState);
        var newPanel = GetPanelForState(newState);
        if (newPanel == null)
            return false;

        double width = ContentGrid.ActualWidth;
        if (width <= 0)
            width = DisplayArea.Primary.WorkArea.Width;

        PanelSlideTransition.Run(oldPanel, newPanel, width, slideToLeft);
        return true;
    }

    public void ClearStrategyChoosers()
    {
        OneListOneStrategyStackPanel.Children.Clear();
    }

    public void AddStrategyChooser(SiteListElement siteList, List<GoodCheckStrategiesList> strategiesLists)
    {
        GoodCheckSitelistStrategyChooserControl checkListChooseControl = new()
        {
            Title = siteList.Name,
            PackName = siteList.PackName,
            FilePath = siteList.Directory,
            StrategiesLists = strategiesLists
        };
        checkListChooseControl.StrategyChanged += (tuple) => ViewModel.SetStrategyForSiteList(tuple.Item1, tuple.Item2);

        OneListOneStrategyStackPanel.Children.Add(checkListChooseControl);
    }

    public async Task ShowErrorAsync(string message)
    {
        try
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = _localizer.GetLocalizedString("ContinuationImpossible"),
                Content = message,
                PrimaryButtonText = _localizer.GetLocalizedString("OkButtonText"),
                XamlRoot = this.XamlRoot,
            };
            await dialog.ShowQueuedAsync();
            CreateConfigUtilWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
            window.Close();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(CreateViaGoodCheck), exception.ToString());
        }
    }

    public void LeaveWizard()
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            CreateConfigUtilWindow.Instance.Close();
        }
    }

    public void OpenWorkPage()
    {
        Frame.Navigate(typeof(GoodCheckWorkPage));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            ViewModel.SetComponent(Parameter.Get("componentId"));
        }

        ViewModel.Start();
    }

    private UIElement GetPanelForState(GoodCheckWizardState s)
    {
        return s switch
        {
            GoodCheckWizardState.PrepareLoading => LoadingStackPanel,
            GoodCheckWizardState.LetsBegin => LetsBeginStackPanel,
            GoodCheckWizardState.SiteListsSettings => GoodCheckSettingsStackPanel,
            GoodCheckWizardState.StrategyListSettings => ChooseStrategiesListStackPanel,
            GoodCheckWizardState.AdditionalSettings => AdditionalSettingsStackPanel,
            _ => null
        };
    }

    private void SiteListTemplates_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SiteListTemplatesStackPanel.Children.Clear();
            return;
        }

        foreach (SiteListElement item in e.NewItems?.OfType<SiteListElement>() ?? [])
        {
            var btn = new SitelistButton
            {
                Title = item.Name,
                PackName = item.PackName,
                PackId = item.PackId,
                Directory = item.Directory,
                Tag = item,
            };

            btn.Click += () =>
            {
                SelectSiteListFlyout.Hide();
                AddSiteListElement(item);
            };

            SiteListTemplatesStackPanel.Children.Add(btn);
        }
    }

    private void SelectedSiteLists_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                SelectedSitelistStackPanel.Children.Clear();
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (SiteListElement element in e.OldItems.OfType<SiteListElement>())
                {
                    var target = SelectedSitelistStackPanel.Children
                        .OfType<GoodCheckSitelistButton>()
                        .FirstOrDefault(c => ReferenceEquals(c.Tag, element));
                    if (target != null)
                    {
                        target.RemoveElement = null;
                        SelectedSitelistStackPanel.Children.Remove(target);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Add:
                foreach (SiteListElement element in e.NewItems.OfType<SiteListElement>())
                {
                    var btn = new GoodCheckSitelistButton
                    {
                        Title = element.Name,
                        PackName = element.PackName,
                        FilePath = element.Directory,
                        Tag = element,
                    };

                    btn.RemoveElement = () =>
                    {
                        _ = ViewModel.RemoveSiteListElementAsync(element);
                    };

                    SelectedSitelistStackPanel.Children.Add(btn);
                }
                break;
        }
    }

    private void AddSiteListElement(SiteListElement site)
    {
        CloseStoryboard.Begin();
        ViewModel.AddSiteListElement(site);
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.GoBack();
    }

    private async void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GoForwardAsync();
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void SelectedSitelistScrollViewer_LayoutUpdated(object sender, object e)
    {
        SelectedSitelistScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - GoodCheckSettingsTextContent.ActualHeight;
        SelectedSitelistScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - GoodCheckSettingsTextContent.ActualHeight;
    }

    private void SelectedSitelistStackPanel_LayoutUpdated(object sender, object e)
    {
        ViewModel.RefreshSiteListsState();
    }

    private void FileIncorrectWarning_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void FileIncorrectWarning_Closing(InfoBar sender, InfoBarClosingEventArgs args)
    {
        args.Cancel = true;

        EventHandler<object> onDone = null;
        onDone = (s, ev) =>
        {
            CloseStoryboard.Completed -= onDone;
            InfoBarTranslate.X = 200;
            sender.Opacity = 0;
        };

        CloseStoryboard.Completed += onDone;
        CloseStoryboard.Begin();
    }

    private void CloseStoryboard_Completed(object sender, object e)
    {
        FileIncorrectWarning.Visibility = Visibility.Collapsed;
    }

    private void OneListOneStrategyScrollViewer_LayoutUpdated(object sender, object e)
    {
        OneListOneStrategyScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - ChooseStrategiesListTextContent.ActualHeight;
        OneListOneStrategyScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - ChooseStrategiesListTextContent.ActualHeight;
    }

    private void AdditionalSettingsScrollViewer_LayoutUpdated(object sender, object e)
    {
        AdditionalSettingsScrollViewer.MaxHeight = GoodCheckSettingsStackPanel.ActualHeight - AdditionalSettingsListTextContent.ActualHeight;
        AdditionalSettingsScrollViewer.Height = GoodCheckSettingsStackPanel.ActualHeight - AdditionalSettingsListTextContent.ActualHeight;
    }

    private async void ChooseFileButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = string.Empty;

        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Title = _localizer.GetLocalizedString("ChooseSiteList");
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Filter = $"{_localizer.GetLocalizedString("TextFiles")} (*.txt)|*.txt";
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

        SiteListElement siteListElement = await ViewModel.ImportSiteListFileAsync(filePath);

        if (siteListElement == null)
        {
            FileIncorrectWarning.IsOpen = true;
            FileIncorrectWarning.Visibility = Visibility.Visible;
            OpenStoryboard.Begin();
            return;
        }

        AddSiteListElement(siteListElement);
    }
}
