using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CDPIUI.AddOns.HostsFileEditor;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Helper.AddOns;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.EditHostsFile;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : TemplatePage
{
    private ILocalizer _localizer = Localizer.Get();
    public MainPage()
    {
        InitializeComponent();

        this.Loaded += MainPage_Loaded;
        this.ActualThemeChanged += MainPage_ActualThemeChanged;

        SetState();
    }

    private void SetState()
    {
        bool isFileChanged = SettingsManager.Instance.GetValueOrDefault<bool>("HOSTS", "isFileEdited", defaultValue: false);
        bool isBackupAvailable = SettingsManager.Instance.GetValueOrDefault<bool>("HOSTS", "isBackupAvailable", defaultValue: false);

        StatusFontIcon.Glyph = isFileChanged ? "\uEC61" : "\uEB90";
        StatusTextBlock.Text = isFileChanged ? _localizer.GetLocalizedString("HostsFileStatusEnabled") : _localizer.GetLocalizedString("HostsFileStatusDisabled");

        ReplaceHostsFileSettingsCard.IsEnabled = !isFileChanged;
        ReplaceHostsFileSettingsCard.Description = isFileChanged ? _localizer.GetLocalizedString("HostsFileAreAllreadyEdited") : "";

        RemoveHostsFileSettingsCard.IsEnabled = isFileChanged;
        RemoveHostsFileSettingsCard.Description = isFileChanged ? "" : _localizer.GetLocalizedString("HostsFileNotEdited");

        RecoverHostsFileSettingsCard.IsEnabled = isBackupAvailable;
        RecoverHostsFileSettingsCard.Description = isBackupAvailable ? "" : _localizer.GetLocalizedString("NoBackupAvailable");
    }

    private void SetLoadingMode(bool isLoading)
    {
        LoadingGrid.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = !isLoading;
        if (isLoading)
        {
            ReplaceHostsFileSettingsCard.IsEnabled = false;
            RemoveHostsFileSettingsCard.IsEnabled = false;
            RecoverHostsFileSettingsCard.IsEnabled = false;
        }
        else
        {
            SetState();
        }
    }

    private void MainPage_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateCriticalPointerOverColor();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCriticalPointerOverColor();
    }

    private void UpdateCriticalPointerOverColor()
    {
        var baseColor = (Color)((App)Application.Current).Resources["SystemFillColorCriticalBackground"];
        bool isDarkTheme = this.ActualTheme == ElementTheme.Dark;
        float blendFactor = isDarkTheme ? 0.04f : 0.95f;

        Color pointerOverColor = BlendWithWhite(baseColor, blendFactor);

        if (isDarkTheme)
            SettingsCardBorderBrushPointerOverSolidColorBrush.Color = pointerOverColor;
        else
            SettingsCardBorderBrushPointerOverSolidColorBrush.Color = (Color)((App)Application.Current).Resources["CardStrokeColorDefault"];
        SettingsCardBackgroundPointerOver.Color = pointerOverColor;
    }

    private Color BlendWithWhite(Color baseColor, float factor)
    {
        if (baseColor.A == 0)
            baseColor = Color.FromArgb(255, 232, 17, 35);

        byte r = (byte)Math.Clamp(baseColor.R + (255 - baseColor.R) * factor, 0, 255);
        byte g = (byte)Math.Clamp(baseColor.G + (255 - baseColor.G) * factor, 0, 255);
        byte b = (byte)Math.Clamp(baseColor.B + (255 - baseColor.B) * factor, 0, 255);

        return Color.FromArgb(255, r, g, b);
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        Commands.CommandsHandler.HandleCommand(
            "cdpiui://Help/Utils/EditHostsFileUtility/");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var window = ((App)Application.Current).GetCurrentWindowFromType<EditHostFileWindow>();
        window?.Close();
    }

    private async void ReplaceHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        SetLoadingMode(true);
        string result = "ERR_FLASHLIGHT_BISY";
        try
        {
            result = ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", await HostsFileEditService.AddDomains());
        }
        catch (Exception ex)
        {
            result = ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", ex);
        }
        finally
        {
            if (result == "HOSTEDIT_SUCCESS (0x00000000)")
            {
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isFileEdited", true);
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isBackupAvailable", true);
            }
            else if (!result.StartsWith("HOSTEDIT_OPERATION_CANCELLED_BY_USER"))
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(_localizer.GetLocalizedString("ErrorHappensWhenTryingAddDomainsToHostsFile"), result, this.XamlRoot);
            }

            SetLoadingMode(false);
        }
    }

    private async void RemoveHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        SetLoadingMode(true);
        string result = "ERR_FLASHLIGHT_BISY";
        try
        {
            result = ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", await HostsFileEditService.RemoveDomains());
        }
        catch (Exception ex)
        {
            result = ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", ex);
        }
        finally
        {
            if (result == "HOSTEDIT_SUCCESS (0x00000000)")
            {
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isFileEdited", false);
            }
            else if (!result.StartsWith("HOSTEDIT_OPERATION_CANCELLED_BY_USER"))
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(_localizer.GetLocalizedString("ErrorHappensWhenTryingRemoveDomainsToHostsFile"), result, this.XamlRoot);
            }

            SetLoadingMode(false);
        }
    }

    private async void RecoverHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        SetLoadingMode(true);
        string result = "ERR_FLASHLIGHT_BISY";
        try
        {
            result = ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", await HostsFileEditService.RestoreDomains());
        }
        catch (Exception ex)
        {
            result = ErrorsHelper.Convertor.GetPrettyErrorCode("HOSTEDIT", ex);
        }
        finally
        {
            if (result == "HOSTEDIT_SUCCESS (0x00000000)")
            {
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isFileEdited", false);
                SettingsManager.Instance.SetValue<bool>("HOSTS", "isBackupAvailable", false);
            }
            else if (!result.StartsWith("HOSTEDIT_OPERATION_CANCELLED_BY_USER"))
            {
                ErrorContentDialog dialog = new();
                await dialog.ShowErrorDialogAsync(_localizer.GetLocalizedString("ErrorHappensWhenTryingRecoverHostsFile"), result, this.XamlRoot);
            }

            SetLoadingMode(false);
        }
    }

    private void OpenHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
    {
        HostsFileEditHelper.EditHostsFile(this.XamlRoot);

    }
}
