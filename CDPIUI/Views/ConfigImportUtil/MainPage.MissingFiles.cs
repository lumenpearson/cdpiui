using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.Shared.Extensions;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI.Views.ConfigImportUtil;

public sealed partial class MainPage
{
    private void ShowMissingFiles()
    {
        UtilityButtons.IsLoading = false;

        MainContent.GoTo(MissingFilesStep);

        NextButton.Content = _localizer.GetLocalizedString("ConfigImportContinueButton");
        UpdateMissingFilesNextState();
    }

    private void UseSuggestedFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
        {
            item.UseSuggestion();
            _missingFiles.Remove(item);
            UpdateMissingFilesNextState();
        }
    }

    private void ChooseReplacementButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConfigImportMissingFileViewModel item })
            return;

        string extension = Path.GetExtension(item.MissingPath);
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = _localizer.GetLocalizedString("ConfigImportChooseReplacementTextBlock.Text"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = string.IsNullOrWhiteSpace(extension)
                ? $"{_localizer.GetLocalizedString("AllSupported")} (*.*)|*.*"
                : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}|{_localizer.GetLocalizedString("AllSupported")} (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        item.UseReplacement(dialog.FileName);
        _missingFiles.Remove(item);
        UpdateMissingFilesNextState();
    }

    private void UseEmptyFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
        {
            item.UseEmptyFile();
            _missingFiles.Remove(item);
            UpdateMissingFilesNextState();
        }
    }

    private void UpdateMissingFilesNextState() =>
        NextButton.IsEnabled =
        _missingFiles.Count == 0
        || (_missingFiles.Count > 0 && _missingFiles.All(item => item.IsResolved))
        || (_missingFiles.Count > 0 && _missingFiles.All(item => item.HasSuggestion));
}
