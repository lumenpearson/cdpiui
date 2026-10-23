using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Controls.Dialogs.EditConfig;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Default;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Helper.WindowHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ConfigMakerWindow : TemplateWindow
{
    public static async Task EditConfig(ConfigItem configItem)
    {
        try
        {
            if (configItem?.target == null || configItem.target.Count == 0) return;
            var window = await ((App)Application.Current).SafeCreateNewWindow<ConfigMakerWindow>(activate: false);
            window.componentSelectionRequested = true;
            App.ActivateWindow(window);
            if (await window.IsExitAvailable())
            {
                await window.OpenConfigFile(configItem);
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerWindow), exception.ToString());
        }
    }

    public static async Task CreateForComponentAsync(string componentId)
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<ConfigMakerWindow>(activate: false);
        window.componentSelectionRequested = true;
        App.ActivateWindow(window);
        if (!await window.IsExitAvailable()) return;
        await window.ConfigMaker.SetComponentAsync(componentId);
        await window.ConfigMaker.NewDocumentAsync();
    }

    private bool componentSelectionRequested;
    private ILocalizer localizer = Localizer.Get();

    public ConfigMakerWindow()
    {
        InitializeComponent();

        WindowsPositionHelper.TrySetMicaBackdrop(true, this, RootGrid);

        WindowTitle = Localizer.Get().GetLocalizedString("ConfigMakerWindowTitle");
        IconUri = @"Assets/Icons/Edit.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        WindowMinSize = new System.Windows.Size(960, 620);
        ConfigMaker.DocumentStateChanged += ConfigMaker_StateChanged;
        ConfigMaker.PanelStateChanged += ConfigMaker_StateChanged;
        ConfigMaker.TestStateChanged += ConfigMaker_StateChanged;
        ConfigMaker.StatusNotificationRequested += ConfigMaker_StatusNotificationRequested;
        Closed += ConfigMakerWindow_Closed;
        ConfigMaker.Loaded += ConfigMaker_Loaded;
        UpdateActionMenu();
    }

    private void ConfigMaker_Loaded(object sender, RoutedEventArgs e)
    {
        _ = Load(); // Load catches and logs its own errors.
    }

    private async Task Load()
    {
        try
        {
            if (!string.IsNullOrEmpty(Id))
            {
                await ConfigMaker.SetComponentAsync(Id);
                await ConfigMaker.NewDocumentAsync();
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigMakerWindow), exception.ToString());
        }
    }

    private void ConfigMaker_StatusNotificationRequested(object sender, Controls.Universal.StatusNotificationRequestedEventArgs e)
    {
        NotificationControl.Show(e.Severity, e.Title, e.Message);
    }

    public void OpenComponent(string componentId, string commandText = "")
    {
        ConfigMaker.ComponentId = componentId ?? string.Empty;
        ConfigMaker.CommandText = commandText ?? string.Empty;
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (componentSelectionRequested || !string.IsNullOrWhiteSpace(ConfigMaker.ComponentId))
        {
            return;
        }

        componentSelectionRequested = true;
        IReadOnlyList<ConfigMakerComponentInfo> components =
            ConfigMakerComponentCatalog.GetAvailableComponents();
        
        if (components.Count == 0)
        {
            ContentDialog unavailableDialog = new()
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerSelectComponentDialogTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerNoComponentsDialogMessage"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
            };
            await unavailableDialog.ShowQueuedAsync();
            Close();
            return;
        }

        await ShowWelcomeDialog();
    }

    

    private bool closeConfirmed;
    private bool isExitDialogOpened;

    private async void ConfigMakerWindow_Closed(object sender, WindowEventArgs args)
    {
        if (!closeConfirmed && HasUnsavedChanges())
        {
            // Handled must be set before the first await, otherwise the window is already closing.
            args.Handled = true;
            if (isExitDialogOpened)
                return;

            isExitDialogOpened = true;
            bool confirmed;
            try
            {
                confirmed = await ShowAskExitDialog();
            }
            finally
            {
                isExitDialogOpened = false;
            }

            if (!confirmed)
                return;

            closeConfirmed = true;
            Close();
            return;
        }

        Closed -= ConfigMakerWindow_Closed;
        ConfigMaker.DocumentStateChanged -= ConfigMaker_StateChanged;
        ConfigMaker.PanelStateChanged -= ConfigMaker_StateChanged;
        ConfigMaker.TestStateChanged -= ConfigMaker_StateChanged;
        ConfigMaker.StatusNotificationRequested -= ConfigMaker_StatusNotificationRequested;
        ConfigMaker.Loaded -= ConfigMaker_Loaded;
        await ConfigMaker.StopTestAsync();
    }

    private void ConfigMaker_StateChanged(object sender, EventArgs e) => UpdateActionMenu();

    private void UpdateActionMenu()
    {
        PresetFilesPanelMenuItem.IsChecked = ConfigMaker.IsPresetFilesPanelVisible;
        CommandPanelMenuItem.IsChecked = ConfigMaker.IsCommandPanelVisible;
        BottomPanelMenuItem.IsChecked = ConfigMaker.IsBottomPanelVisible;
        StartTestMenuItem.IsEnabled = !ConfigMaker.IsTesting;
        StopTestMenuItem.IsEnabled = ConfigMaker.IsTesting;
    }

    private async void NewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (await IsExitAvailable()) await ConfigMaker.NewDocumentAsync();
    }

    private async void SaveToApplicationMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ConfigMaker.SaveToApplicationAsync();

    private async void SaveTextMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ConfigMaker.SaveTextAsync();

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private void FormatMenuItem_Click(object sender, RoutedEventArgs e) => ConfigMaker.FormatCommand();

    private void PresetFilesPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        ConfigMaker.SetPresetFilesPanelVisible(!ConfigMaker.IsPresetFilesPanelVisible);

    private void CommandPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        ConfigMaker.SetCommandPanelVisible(!ConfigMaker.IsCommandPanelVisible);

    private void BottomPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        ConfigMaker.SetBottomPanelVisible(!ConfigMaker.IsBottomPanelVisible);

    private void ShowOutputMenuItem_Click(object sender, RoutedEventArgs e) => ConfigMaker.ShowOutputTab();

    private void ShowDiagnosticsMenuItem_Click(object sender, RoutedEventArgs e) =>
        ConfigMaker.ShowDiagnosticsTab();

    private void ShowPresetGroupsMenuItem_Click(object sender, RoutedEventArgs e) =>
        ConfigMaker.ShowPresetGroupsTab();

    private async void StartTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ConfigMaker.StartTestAsync();

    private async void StopTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ConfigMaker.StopTestAsync(showCompletionMessage: true);

    private async void RefreshHelpMenuItem_Click(object sender, RoutedEventArgs e) =>
        await ConfigMaker.RefreshHelpAsync();

    private async void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var result = ConfigImportHelper.ImportConfigFromFile();
        if (result.Success && result.Result.IsSuccessful && await IsExitAvailable()) await OpenConfigFile(result.Result.Config);
        if (result.Success)
        {
            foreach (var item in result.Result.Issues)
            {
                NotificationControl.Show(
                    item.Severity == AddOns.ConfigImport.ConfigImportIssueSeverity.Warning ? InfoBarSeverity.Warning : InfoBarSeverity.Error, 
                    item.Code, 
                    ConfigImportIssueText.GetMessage(item));
            }
        }
        
    }

    private void FontMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = ConsoleFontHelper.Instance.ShowFontSettingsDialogForXamlRoot(Content.XamlRoot); // ShowFontSettingsDialogForXamlRoot catches and logs its own errors.
    }

    public async Task OpenConfigFile(ConfigItem configItem)
    {
        if (configItem?.target == null || configItem.target.Count == 0) return;
        await ConfigMaker.SetComponentAsync(configItem.target[0]);
        await ConfigMaker.LoadConfigItem(configItem, applyAutoCorrectorSilently: true);
        ConfigMaker.FormatCommand();
    }

    private async void EditConfigMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectConfigToEditContentDialog dialog = new()
        {
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowQueuedAsync();

        if (dialog.SelectedConfigResult == SelectResult.Selected)
        {
            if (dialog.SelectedConfigItem?.target?.Count > 0 && await IsExitAvailable())
            {
                ConfigMaker.ComponentId = dialog.SelectedConfigItem.target[0];
                await OpenConfigFile(dialog.SelectedConfigItem);
            }
        }
    }

    private async void OpenWelcomeDialog_Click(object sender, RoutedEventArgs e)
    {
        if (await IsExitAvailable())
        {
            await ShowWelcomeDialog();
        }
    }

    private bool HasUnsavedChanges() =>
        !string.IsNullOrEmpty(ConfigMaker.CommandText) ||
        ConfigMaker.HasVariables ||
        ConfigMaker.HasPresetFiles;

    private async Task<bool> IsExitAvailable()
    {
        if (HasUnsavedChanges())
        {
            
            var result = await ShowAskExitDialog();
            if (result) await ConfigMaker.LoadConfigItem(new ConfigItem());
            return result;
        }
        return true;
    }

    private async Task<bool> ShowAskExitDialog()
    {
        ContentDialog exitDialog = new()
        {
            Title = localizer.GetLocalizedString("Exit"),
            Content = localizer.GetLocalizedString("ExitAsk"),
            PrimaryButtonText = localizer.GetLocalizedString("Yes"),
            CloseButtonText = localizer.GetLocalizedString("No"),
            XamlRoot = this.Content.XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
        };
        var result = await exitDialog.ShowQueuedAsync();
        if (result == ContentDialogResult.Primary)
        {
            return true;
        }
        return false;
    }

    private async Task ShowWelcomeDialog()
    {
        EditConfigWindowWelcomeContentDialog dialog = new()
        {
            XamlRoot = this.Content.XamlRoot,
        };

        await dialog.ShowQueuedAsync();

        if (!string.IsNullOrEmpty(dialog.ResultSelectedComponentId))
        {
            await ConfigMaker.SetComponentAsync(dialog.ResultSelectedComponentId);
        }

        switch (dialog.Result)
        {
            case EditConfigWelcomeWindowResult.EditConfig:
                await OpenConfigFile(dialog.ResultObject as ConfigItem);
                break;
            case EditConfigWelcomeWindowResult.ImportConfigFromFile:
                await OpenConfigFile(dialog.ResultObject as ConfigItem);
                break;
            case EditConfigWelcomeWindowResult.EditConfigKit:
                CommandsHandler.HandleCommand($"cdpiui://Tools/EditConfigKit/{dialog.ResultObject}");
                this.Close();
                break;
            case EditConfigWelcomeWindowResult.Exit:
                this.Close();
                break;
        }
    }

    
}
