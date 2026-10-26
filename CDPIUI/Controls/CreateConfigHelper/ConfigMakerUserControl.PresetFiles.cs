using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.System;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace CDPIUI.Controls.CreateConfigHelper;

// The actions of the preset file tree: open, show, add, remove and replace missing files.
public sealed partial class ConfigMakerUserControl
{
    public void AddPresetFiles(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        int added = _presetResources.AddFiles(filePaths);
        if (added > 0)
        {
            AttachExistingPresetFiles();
            RebuildPresetStructure();
            SetPresetFilesPanelVisible(true);
            ShowEditorMessage(
                string.Format(
                    _localizer.GetLocalizedString("ConfigMakerPresetFilesAddedMessage"),
                    added),
                InfoBarSeverity.Success);
        }
    }

    private async void ReplacePresetFileItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
            if (file == null)
            {
                return;
            }

            string missingPath = _presetResources.TryResolvePath(file.Path);
            if (string.IsNullOrWhiteSpace(missingPath))
            {
                ShowEditorMessage(
                    _localizer.GetLocalizedString("ConfigMakerPresetFileInvalidPathMessage"),
                    InfoBarSeverity.Error);
                return;
            }
            if (File.Exists(missingPath))
            {
                RebuildPresetStructure();
                return;
            }

            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = _localizer.GetLocalizedString("ConfigMakerPresetFileReplaceDialogTitle"),
                Content = CreateReplacementDialogContent(missingPath),
                PrimaryButtonText = _localizer.GetLocalizedString("ConfigMakerPresetFileApplyAutoCorrectionButton"),
                SecondaryButtonText = _localizer.GetLocalizedString("ConfigMakerPresetFileChooseManuallyButton"),
                CloseButtonText = _localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            ContentDialogResult result = await dialog.ShowQueuedAsync();
            if (result == ContentDialogResult.Primary)
            {
                await AutoCorrectPresetFileAsync(file, missingPath);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                ChooseAndApplyPresetFileReplacement(file, missingPath);
            }
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private async Task AutoCorrectPresetFileAsync(ConfigMakerPresetFileInfo file, string missingPath)
    {
        ConfigImportAutoCorrector autoCorrector = new();
        bool suggestEmptyFile = autoCorrector.ShouldSuggestEmptyFile(missingPath);
        if (suggestEmptyFile)
        {
            TryCreateEmptyPresetFile(missingPath);
            return;
        }

        ConfigImportResult autoCorrectResult = _presetResources.CreateAutoCorrectResult(missingPath);
        string suggestion = await Task.Run(() => autoCorrector.FindReplacement(
            autoCorrectResult,
            missingPath));
        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            ApplyPresetFileReplacement(file, suggestion);
            return;
        }

        ShowEditorMessage(
            _localizer.GetLocalizedString("ConfigMakerPresetFileNoSuggestionMessage"),
            InfoBarSeverity.Warning);
        ChooseAndApplyPresetFileReplacement(file, missingPath);
    }

    private void ChooseAndApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string missingPath)
    {
        string selectedPath = ChoosePresetFileReplacement(missingPath);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            ApplyPresetFileReplacement(file, selectedPath);
        }
    }

    private FrameworkElement CreateReplacementDialogContent(string missingPath)
    {
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = string.Format(
                _localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"),
                missingPath),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = _localizer.GetLocalizedString("ConfigMakerPresetFileReplacementChoiceMessage"),
            TextWrapping = TextWrapping.Wrap,
        });
        return content;
    }

    private string ChoosePresetFileReplacement(string missingPath)
    {
        string extension = Path.GetExtension(missingPath);
        using System.Windows.Forms.OpenFileDialog dialog = new()
        {
            Title = _localizer.GetLocalizedString("ConfigImportChooseReplacementTextBlock.Text"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = string.IsNullOrWhiteSpace(extension)
                ? $"{_localizer.GetLocalizedString("AllSupported")} (*.*)|*.*"
                : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}|{_localizer.GetLocalizedString("AllSupported")} (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.FileName
            : string.Empty;
    }

    private bool TryCreateEmptyPresetFile(string missingPath, bool showStatus = true)
    {
        try
        {
            ConfigMakerPresetResources.CreateEmptyFile(missingPath);
            RebuildPresetStructure();
            if (showStatus)
            {
                ShowEditorMessage(
                    string.Format(
                        _localizer.GetLocalizedString("ConfigMakerPresetFileEmptyCreatedMessage"),
                        missingPath),
                    InfoBarSeverity.Success);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (showStatus)
            {
                ShowPresetFileReplaceFailed(ex);
            }
            return false;
        }
    }

    private void ApplyPresetFileReplacement(ConfigMakerPresetFileInfo file, string replacementPath) =>
        TryApplyPresetFileReplacement(file, replacementPath, showStatus: true);

    private bool TryApplyPresetFileReplacement(
        ConfigMakerPresetFileInfo file,
        string replacementPath,
        bool showStatus)
    {
        try
        {
            string fullReplacementPath = Path.GetFullPath(replacementPath);
            if (!File.Exists(fullReplacementPath))
            {
                throw new FileNotFoundException(null, fullReplacementPath);
            }

            if (_presetResources.TryUpdateAttachedResource(file, fullReplacementPath))
            {
                RebuildPresetStructure();
                if (showStatus)
                {
                    ShowPresetFileReplaced(fullReplacementPath);
                }
                return true;
            }

            ConfigMakerPresetFileReplacement replacement = _presetResources.ReplaceReferences(
                file,
                fullReplacementPath) ?? throw new InvalidOperationException(
                    _localizer.GetLocalizedString("ConfigMakerPresetFileReferenceNotFoundMessage"));

            if (_usesExplicitPresetStructure)
            {
                _explicitPresetFiles = _explicitPresetFiles
                    .Select(item => ReferenceEquals(item, file) ||
                        (item.Kind == file.Kind && ConfigFileReferences.PathsEqual(item.Path, file.Path))
                            ? item with
                            {
                                Name = Path.GetFileName(fullReplacementPath),
                                Path = replacement.Reference,
                                Folder = ConfigFileReferences.GetDisplayFolder(replacement.Reference),
                            }
                            : item)
                    .ToArray();
            }
            if (replacement.CommandChanged)
            {
                CommandText = ComponentCommandLineFormatter.FormatByFlags(replacement.UpdatedCommandText);
            }
            RebuildPresetStructure();
            PresetFileReplaced?.Invoke(
                this,
                new ConfigMakerPresetFileReplacedEventArgs(
                    CommandText,
                    file.Path,
                    replacement.Reference));
            if (showStatus)
            {
                ShowPresetFileReplaced(fullReplacementPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (showStatus)
            {
                ShowPresetFileReplaceFailed(ex);
            }
            return false;
        }
    }

    private void ShowPresetFileReplaced(string fullReplacementPath) =>
        ShowEditorMessage(
            string.Format(
                _localizer.GetLocalizedString("ConfigMakerPresetFileReplacedMessage"),
                fullReplacementPath),
            InfoBarSeverity.Success);

    private void ShowPresetFileReplaceFailed(Exception exception) =>
        ShowEditorMessage(
            string.Format(
                _localizer.GetLocalizedString("ConfigMakerPresetFileReplaceFailedMessage"),
                exception.Message),
            InfoBarSeverity.Error);

    private async void OpenPresetFileItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
            if (file == null)
            {
                return;
            }

            string path = _presetResources.ResolvePath(file.Path);
            if (!File.Exists(path))
            {
                ShowPresetFileMissing(path);
                return;
            }

            if (file.Kind == ConfigMakerPresetFileKind.SiteList ||
                string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                if (TextFileOpenPreferences.ShouldAskForApplication())
                {
                    EditSitelistAskApplicationContentDialog dialog = new()
                    {
                        XamlRoot = XamlRoot,
                        FilePath = path,
                    };
                    await dialog.ShowQueuedAsync();
                    if (dialog.IsSuccess)
                    {
                        TextFileOpenPreferences.RememberDialogShown();
                    }
                    return;
                }

                ShellHelper.OpenFile(path);
                return;
            }

            ShellHelper.OpenFileInDefaultApp(path);
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private void ShowPresetFileInFolderItem_Click(object sender, RoutedEventArgs e)
    {
        ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
        if (file == null)
        {
            return;
        }

        string path = _presetResources.ResolvePath(file.Path);
        if (!File.Exists(path))
        {
            ShowPresetFileMissing(path);
            return;
        }
        ShellHelper.LookupFileInDirectory(path);
    }

    private void ShowPresetFileMissing(string path) =>
        ShowEditorMessage(
            string.Format(_localizer.GetLocalizedString("ConfigMakerPresetFileMissingMessage"), path),
            InfoBarSeverity.Warning);

    private void AddPresetFileButton_Click(object sender, RoutedEventArgs e)
    {
        using System.Windows.Forms.OpenFileDialog dialog = new()
        {
            Title = _localizer.GetLocalizedString("ConfigMakerAddPresetFileDialogTitle"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = _localizer.GetLocalizedString("ConfigMakerPresetResourceFileFilter"),
            Multiselect = true,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }
        AddPresetFiles(dialog.FileNames);
    }

    private void EditorPanel_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = _localizer.GetLocalizedString("ConfigMakerAddPresetFileDialogTitle");
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
    }

    private async void EditorPanel_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            IReadOnlyList<IStorageItem> droppedItems = await e.DataView.GetStorageItemsAsync();
            AddPresetFiles(droppedItems
                .OfType<StorageFile>()
                .Select(file => file.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path)));
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
        finally
        {
            e.Handled = true;
        }
    }

    private async void RemovePresetFileItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigMakerPresetFileInfo file = GetPresetFileFromMenuSender(sender);
            ConfigMakerPresetResource resource = file == null ? null : _presetResources.FindAttached(file.Path);
            if (resource == null)
            {
                return;
            }
            if (_presetResources.IsReferenced(resource))
            {
                ContentDialog confirmation = new()
                {
                    XamlRoot = XamlRoot,
                    Title = _localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogTitle"),
                    Content = _localizer.GetLocalizedString("ConfigMakerRemoveReferencedFileDialogMessage"),
                    PrimaryButtonText = _localizer.GetLocalizedString("Remove"),
                    CloseButtonText = _localizer.GetLocalizedString("Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await confirmation.ShowQueuedAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
            }
            _presetDocument.Resources.Remove(resource);
            RebuildPresetStructure();
            ScheduleDiagnostics();
        }
        catch (Exception exception)
        {
            ReportHandlerError(exception);
        }
    }

    private void PresetFilesTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (IsEditorReadOnly ||
            PresetFilesTreeView.SelectedNode?.Content is not ConfigMakerPresetFileTreeItem item ||
            item.File == null)
        {
            return;
        }
        string reference = item.File.Path.Trim().Trim('"');
        if (!reference.StartsWith("preset://", StringComparison.OrdinalIgnoreCase))
        {
            string resolvedPath = _presetResources.TryResolvePath(reference);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            {
                ConfigMakerPresetResource resource = _presetResources.GetOrCreate(
                    Path.GetFullPath(resolvedPath),
                    item.File.Kind,
                    out bool resourceAdded);
                if (resourceAdded)
                {
                    RebuildPresetStructure();
                }
                reference = resource.Reference;
            }
        }
        InsertAtCursor($"\"{reference}\"");
    }

    private static ConfigMakerPresetFileInfo GetPresetFileFromMenuSender(object sender)
    {
        if (sender is not FrameworkElement element)
        {
            return null;
        }
        if (element.Tag is ConfigMakerPresetFileInfo taggedFile)
        {
            return taggedFile;
        }
        if (element.DataContext is TreeViewNode node &&
            node.Content is ConfigMakerPresetFileTreeItem nodeItem)
        {
            return nodeItem.File;
        }
        return (element.DataContext as ConfigMakerPresetFileTreeItem)?.File;
    }
}
