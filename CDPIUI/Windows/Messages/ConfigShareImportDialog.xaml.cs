using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using CDPIUI.Default;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Messages;

public sealed partial class ConfigShareImportDialog : TemplateWindow
{
    private readonly ConfigShareService _service = new();
    private ConfigSharePackage _package;
    private bool _busy;
    private bool _closed;
    private bool _initialized;
    private bool _installed;
    private bool _isDragDropImport;
    private Window _modalOwner;

    private readonly ILocalizer _localizer = Localizer.Get();

    private List<string> Modes = [];

    public ConfigShareImportDialog()
    {
        InitializeComponent();
        WindowTitle = _localizer.GetLocalizedString("ConfigShareImportTitle");
        IconUri = @"Assets/favicon.ico";
        CustomTitleBarUserControl = TitleBarControl;
        DisableResizeFeature();

        Modes = [_localizer.GetLocalizedString("AsKit"), _localizer.GetLocalizedString("AsNewKit")];
        ModeSelectionCombobox.ItemsSource = Modes;
        ModeSelectionCombobox.SelectedIndex = 0;

        ProgressText.Text = _localizer.GetLocalizedString("ConfigShareReading");

        DestinationCombo.SelectionChanged += (_, _) => UpdateDestination();
        Closed += (_, _) => { _closed = true; if (!_busy) _package?.Dispose(); };
        AppWindow.Closing += (_, args) => args.Cancel = _busy;
        Activated += OnFirstActivated;

        _initialized = true;
    }

    public void ConfigureForDragDrop(Window owner)
    {
        _isDragDropImport = true;
        _modalOwner = owner;
        AlwaysAskCheckBox.Visibility = Visibility.Visible;
        AlwaysAskCheckBox.IsChecked = SettingsManager.Instance.GetValueOrDefault<bool>(
            "CONFIGSHARE", "AlwaysAskIfDragNDropConfigAdded", defaultValue: true);
    }

    private void AlwaysAskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isDragDropImport)
            SettingsManager.Instance.SetValue("CONFIGSHARE", "AlwaysAskIfDragNDropConfigAdded", AlwaysAskCheckBox.IsChecked == true);
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        ((App)Application.Current).ShowWindowModalAsync(this, _modalOwner);
    }

    public async Task SetFileAsync(string path)
    {
        if (_package != null)
            return;
        SourcePath.Text = string.Format(_localizer.GetLocalizedString("Source"), path);
        try
        {
            var loaded = await _service.ReadAsync(path);
            if (_closed)
            { loaded.Dispose(); return; }
            _package = loaded;
            PresetName.Text = string.Format(_localizer.GetLocalizedString("StoreSmallAddItemName"), _package.Manifest.Name);

            DeveloperName.Text = string.Format(_localizer.GetLocalizedString("StoreSmallDeveloperText"), _package.Manifest.Developer);

            // The raw startup string, so the user can check what the preset runs before adding it.
            string arguments = _package.Config?.startup_string;
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                ArgumentsHeader.Text = _localizer.GetLocalizedString("ConfigImportArguments");
                PresetArguments.Text = arguments;
                ArgumentsPanel.Visibility = Visibility.Visible;
            }

            KitName.Text = string.Format(_localizer.GetLocalizedString("ConfigShareKitBy"), _package.Manifest.Developer);
            var packs = _service.GetDestinationPacks().ToList();
            foreach (var pack in packs)
                pack.ShortName = pack.Id == SharedConstants.LocalUserItemsId
                    ? _localizer.GetLocalizedString("ConfigShareLocal") : pack.ShortName ?? pack.Name ?? pack.Id;
            DestinationCombo.ItemsSource = packs;
            DestinationCombo.SelectedItem = packs.FirstOrDefault();

            if (packs.Count == 0)
                ModeSelectionCombobox.SelectedItem = Modes[1];

            DestCard.Visibility = Visibility.Visible;
            UpdateDestination();
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) { if (!_closed) ShowError(exception); }
        finally
        {
            if (!_closed)
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void KitName_Changed(object sender, TextChangedEventArgs e) => UpdateDestination();
    private void UpdateDestination()
    {
        if (!_initialized)
            return;
        bool newKit = (string)ModeSelectionCombobox.SelectedItem == Modes[1];
        NameCard.Visibility = newKit ? Visibility.Visible : Visibility.Collapsed;
        DestCard.Visibility = newKit ? Visibility.Collapsed : Visibility.Visible;
        InstallButton.IsEnabled = !_busy && !_installed && _package != null &&
            (newKit ? !string.IsNullOrWhiteSpace(KitName.Text) : DestinationCombo.SelectedItem is DatabaseStoreItem);
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _package == null || _installed)
            return;

        _busy = true;
        UpdateDestination();
        ModeSelectionCombobox.IsEnabled = DestinationCombo.IsEnabled = KitName.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ErrorBar.IsOpen = false;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = _localizer.GetLocalizedString("ConfigShareInstalling");

        InstallButton.IsEnabled = false;

        try
        {
            await ConfigShareUI.OfferComponentInstallAsync(Content.XamlRoot, _package.Config);
            if (_closed)
                return;
            bool newKit = (string)ModeSelectionCombobox.SelectedItem == Modes[1];
            await _service.InstallAsync(_package, _package.Manifest.Name,
                (DestinationCombo.SelectedItem as DatabaseStoreItem)?.Id,
                newKit ? KitName.Text : null);
            _installed = true;
            try
            {
                string componentId = ConfigShareService.GetInstalledComponentId(_package.Config);
                if (componentId != null)
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId)?.ReInitConfigs();
            }
            catch (Exception exception) { Logger.Instance.CreateWarningLog(nameof(ConfigShareImportDialog), $"Preset list refresh: {exception}"); }
            if (_closed)
                return;
            SuccessBar.Message = _localizer.GetLocalizedString("ConfigShareImported");
            SuccessBar.IsOpen = true;
            InstallButton.Visibility = DestinationPanel.Visibility = Visibility.Collapsed;
            CancelButton.Content = _localizer.GetLocalizedString("ConfigShareClose");
            _package.Dispose();
        }
        catch (Exception exception) { if (!_closed) ShowError(exception); }
        finally
        {
            _busy = false;
            if (_closed)
                _package?.Dispose();
            else
            {
                CancelButton.IsEnabled = ModeSelectionCombobox.IsEnabled = DestinationCombo.IsEnabled = KitName.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                UpdateDestination();
            }
        }
    }

    private void ShowError(Exception exception)
    {
        Logger.Instance.CreateWarningLog(nameof(ConfigShareImportDialog), exception.ToString());
        ErrorBar.Message = ConfigShareUI.ErrorText(exception);
        ErrorBar.IsOpen = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { if (!_busy) Close(); }

    private void ModeSelectionCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDestination();
    }
}
