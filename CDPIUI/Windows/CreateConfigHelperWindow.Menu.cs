using System;
using System.Collections.Specialized;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Helper;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI;

// The handlers of the window menu: files, GoodCheck, stores, help and the editor view.
public sealed partial class CreateConfigHelperWindow
{
    private void CreateNewConfigButton_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(CreateNewConfigPage), null, new DrillInNavigationTransitionInfo());
    }

    private async void ImportConfigFromFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SettingsManager.Instance.GetValue<bool>("AD", "ImportConfigFromFile"))
        {
            ImportConfigFromFileDialog dialog = new ImportConfigFromFileDialog() { XamlRoot = this.Content.XamlRoot };
            await dialog.ShowQueuedAsync();
            SettingsManager.Instance.SetValue("AD", "ImportConfigFromFile", true);
        }

        string filePath = CreateConfigHelperFileDialogs.ChooseConfigFile(_localizer);
        if (filePath == null)
        {
            return;
        }

        var (configItem, errorHappens) = ConfigurationService.LoadConfigFromFile(filePath);
        ContentFrame.Navigate(
            typeof(CreateNewConfigPage),
            CreateConfigNavigationParameters.ImportConfig(configItem, errorHappens, filePath),
            new DrillInNavigationTransitionInfo());
    }

    private void EditConfigButton_Click(object sender, RoutedEventArgs e)
    {
        OpenConfigEditPage(true);
    }

    private void OpenGoodCheckReportButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath = CreateConfigHelperFileDialogs.ChooseGoodCheckReport(_localizer);
        if (filePath == null)
        {
            return;
        }

        ContentFrame.Navigate(
            typeof(ViewGoodCheckReportPage),
            CreateConfigNavigationParameters.GoodCheckReport(filePath),
            new DrillInNavigationTransitionInfo());
    }

    private async void RecentGoodCheckSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        RecentGoodCheckSelectionsContentDialog dialog = new RecentGoodCheckSelectionsContentDialog()
        {
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowQueuedAsync();
        if (dialog.SelectedResult == SelectResult.Selected)
        {
            string directory = dialog.SelectedReport;

            ContentFrame.Navigate(
                typeof(ViewGoodCheckReportPage),
                CreateConfigNavigationParameters.GoodCheckReport(directory),
                new DrillInNavigationTransitionInfo());
        }
    }

    private async void BeginNewGoodCheckSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigUtilWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
        // window.NavigateToPage<CreateViaGoodCheck>();
    }

    private async void EditConfigKitButton_Click(object sender, RoutedEventArgs e)
    {
        SelectConfigKitToEditContentDialog dialog = new()
        {
            XamlRoot = this.Content.XamlRoot,
        };

        await dialog.ShowQueuedAsync();

        if (!string.IsNullOrEmpty(dialog.Result))
        {
            EditConfigKit(dialog.Result);
        }
    }

    private async void ComponentsStore_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), new NameValueCollection() { { "categoryId", "C001CS" } }, new SuppressNavigationTransitionInfo());
    }

    private async void AddOnsStore_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), new NameValueCollection() { { "categoryId", "C003AS" } }, new SuppressNavigationTransitionInfo());
    }

    private async void ConfigsStore_Click(object sender, RoutedEventArgs e)
    {
        StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), new NameValueCollection() { { "categoryId", "C002CS" } }, new SuppressNavigationTransitionInfo());
    }

    private void Store_Click(object sender, RoutedEventArgs e)
    {
        _ = ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
    }

    private async void ExtiMenuButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigHelperWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
        window.Close();
    }

    private void CurrentHelpMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        Commands.CommandsHandler.HandleCommand(
            $"cdpiui://Help/CreateConfigHelper/{ContentFrame.SourcePageType.Name}/");
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is CreateNewConfigPage configPage)
        {
            configPage.OpenSearch();
        }
    }

    private void SearchAndReplace_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is CreateNewConfigPage configPage)
        {
            configPage.OpenSearchAndReplace();
        }
    }

    private void EditorBackgroundMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string key = "Black";
        if (MicaBackgroundMenuItem.IsChecked)
        {
            key = "Mica";
        }
        else if (MicaAltMenuItem.IsChecked)
        {
            key = "MicaTransparent";
        }
        else if (MicaSmokeMenuItem.IsChecked)
        {
            key = "MicaSmoke";
        }

        ViewModel.SetEditorBackground(key);

        if (ContentFrame.Content is CreateNewConfigPage configPage)
        {
            configPage.ChangeBackground();
        }
    }

    private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is CreateNewConfigPage configPage)
        {
            configPage.ChangeZoom(5);
        }
    }

    private void ZoomOutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is CreateNewConfigPage configPage)
        {
            configPage.ChangeZoom(-5);
        }
    }

    private void ZoomReturnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is CreateNewConfigPage configPage)
        {
            configPage.ChangeZoom(0);
        }
    }
}
