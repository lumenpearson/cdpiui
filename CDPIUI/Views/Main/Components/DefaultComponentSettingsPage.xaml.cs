using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using System.Threading.Tasks;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.ViewModels.ComponentViews;
using CDPIUI.Views.CreateConfigUtil;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;

namespace CDPIUI.Views.Main.Components;

public sealed partial class DefaultComponentSettingsPage : TemplatePage, IDefaultComponentSettingsView
{
    private readonly ObservableCollection<UIElement> _tiles = new();
    private readonly ILocalizer _localizer = Localizer.Get();

    public DefaultComponentSettingsPage()
    {
        ViewModel = new DefaultComponentSettingsViewModel(DispatcherQueue, this);

        InitializeComponent();

        Loaded += PresetPage_Loaded;
        Unloaded += PresetPage_Unloaded;

        StaggeredRepeater.ItemsSource = _tiles;

        DataContext = this;
    }

    public DefaultComponentSettingsViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            ViewModel.ComponentId = Parameter.Get("componentId") ?? string.Empty;
            EmptyPlaceholder.StoreId = ViewModel.ComponentId;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        PresetPage_Unloaded(this, null);
    }

    public void ClearTiles() => _tiles.Clear();

    public void AddTile(SettingsTile tile) => _tiles.Add(CreateSettingTile(tile, ViewModel.HandleTileAction));

    public async Task ShowAppliedArgsAsync(string dialogTitle, List<string> args)
    {
        ViewApplyArgsContentDialog dialog = new()
        {
            Title = _localizer.GetLocalizedString("AppliedFlagsDialogTitle"),
            DialogTitle = dialogTitle,
            Args = args,
            XamlRoot = this.XamlRoot,
        };
        await dialog.ShowQueuedAsync();
    }

    public async Task ShowErrorAsync(string message, string details)
    {
        ErrorContentDialog dialog = new();
        await dialog.ShowErrorDialogAsync(message, details, this.XamlRoot);
    }

    public async Task<SiteListReplacement> ChooseSiteListAsync(string listTitle, string packId, string fileName, bool isIpSet)
    {
        ChangeSiteListContentDialog changeListContentDialog = new()
        {
            XamlRoot = this.XamlRoot,
            ListTitle = listTitle,
            PackId = packId,
            FileName = fileName,
            IsIpSet = isIpSet,
        };
        await changeListContentDialog.ShowQueuedAsync();

        return changeListContentDialog.IsDialogFinishedSuccessfully
            ? new SiteListReplacement(changeListContentDialog.SelectionType, changeListContentDialog.NewFileName)
            : null;
    }

    public async Task<bool> AskFileOpenApplicationAsync(string filePath)
    {
        EditSitelistAskApplicationContentDialog editSitelistAskApplicationContentDialog = new()
        {
            XamlRoot = this.XamlRoot,
            FilePath = filePath
        };
        await editSitelistAskApplicationContentDialog.ShowQueuedAsync();
        return editSitelistAskApplicationContentDialog.IsSuccess;
    }

    public async Task<string> EditCommaVariableAsync(string name, string value, AvailableVarValues availableValues)
    {
        var dialog = new EditConfigVarValueDialog(name, value, availableValues)
        {
            XamlRoot = XamlRoot,
        };
        return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary ? dialog.VarValue : null;
    }

    public Task CreateConfigInEditorAsync(string componentId) => ConfigMakerWindow.CreateForComponentAsync(componentId);

    public Task EditConfigAsync(ConfigItem item) => ConfigMakerWindow.EditConfig(item);

    public async Task OpenGoodCheckAsync(string componentId)
    {
        CreateConfigUtilWindow gwindow = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
        gwindow.NavigateToPage<CreateViaGoodCheck>(new NameValueCollection() { { "componentId", componentId } });
    }

    private void PresetPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Load();
    }

    private void PresetPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Unload();
    }

    private async void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SelectConfigAsync(ConfigChooseCombobox.SelectedItem as ConfigSelectorItem, IsLoaded);
    }

    private void ComponentHelpfulLinksUserControl_ConfigEditRequested(object sender, RoutedEventArgs e)
    {
        ViewModel.EditSelectedConfig();
    }

    private void StaggeredRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (!(args.Element is UIElement element))
            return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        if (ViewModel.ShowAnimation)
            visual.Properties.InsertVector3("Translation", new Vector3(0, 50, 0));

        visual.Opacity = 0f;

        element.DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Normal,
            () =>
            {
                bool showAnimation = ViewModel.ShowAnimation;

                var translationAnim = compositor.CreateVector3KeyFrameAnimation();
                translationAnim.Target = "Translation";
                translationAnim.InsertKeyFrame(0f, new Vector3(0, 50, 0));
                translationAnim.InsertKeyFrame(1f, Vector3.Zero);
                translationAnim.Duration = TimeSpan.FromMilliseconds(300);

                if (showAnimation)
                    visual.StartAnimation("Translation", translationAnim);

                var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
                fadeAnim.Target = "Opacity";
                if (showAnimation)
                    fadeAnim.InsertKeyFrame(0f, 0f);
                else
                    fadeAnim.InsertKeyFrame(0.5f, 0.5f);
                fadeAnim.InsertKeyFrame(1f, 1f);
                fadeAnim.Duration = TimeSpan.FromMilliseconds(showAnimation ? 300 : 150);

                visual.StartAnimation("Opacity", fadeAnim);
            });
    }
}
