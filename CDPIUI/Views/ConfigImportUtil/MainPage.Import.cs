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
    public void QueueDroppedFiles(string[] paths)
    {
        _pendingDroppedFiles = paths.ToArray();
        if (IsLoaded)
            _ = ImportPendingFilesAsync();
    }

    private async Task ImportPendingFilesAsync()
    {
        if (_pendingDroppedFiles == null || _importingDroppedFiles || _isUnloaded)
            return;
        var paths = _pendingDroppedFiles;
        _pendingDroppedFiles = null;
        _importingDroppedFiles = true;
        NextButton.Visibility = Visibility.Collapsed;
        try
        { await ImportConfigsAsync(paths); }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(MainPage), exception.ToString());
            if (!_isUnloaded && IsLoaded)
                await new CDPIUI.Controls.Dialogs.Universal.ErrorContentDialog().ShowErrorDialogAsync(
                    _localizer.GetLocalizedString("ConfigShareError"), exception.Message, XamlRoot);
        }
        finally
        {
            _importingDroppedFiles = false;
            if (!_isUnloaded)
            {
                UtilityButtons.IsLoading = false;
                NextButton.Visibility = Visibility.Visible;
            }
        }
    }

    private async Task<bool> ImportConfigsAsync(string[] suppliedPaths = null)
    {
        LoadComponents();
        if (_components.Count == 0)
            return false;
        string[] filePaths = suppliedPaths;
        if (filePaths == null)
        {
            var result = ConfigImportHelper.OpenFileSelectionDialog(true);
            if (!result.Success)
                return false;
            filePaths = result.Result;
        }

        if (filePaths.Length == 0)
            return false;

        UtilityButtons.IsLoading = true;

        MainContent.GoTo(ProgressStep);

        foreach (var item in _results)
            item.Result.SharedPackage?.Dispose();
        _results.Clear();
        _missingFiles.Clear();

        await Task.Run(() => ImportWork(filePaths));
        if (_isUnloaded)
            return false;

        await PrepareSharedComponentsAsync();
        if (_isUnloaded)
            return false;

        await PrepareMissingFilesAsync();
        if (_isUnloaded)
            return false;

        if (_missingFiles.Count > 0)
            ShowMissingFiles();
        else
            ShowCompletion();

        return true;
    }

    private async Task ImportWork(string[] filePaths)
    {
        List<ConfigImportTarget> targets = [.. _components.Select(ConfigImportTargetFactory.Create)];
        for (int index = 0; index < filePaths.Length; index++)
        {

            string text = string.Format(
                _localizer.GetLocalizedString("ConfigImportProgressCounter"),
                index + 1,
                filePaths.Length,
                Path.GetFileName(filePaths[index]));

            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_isUnloaded)
                    UtilityButtons.LoadingStateText = text;
            });

            AnalyzedConfigImport analyzed = await Task.Run(() =>
                _fileAnalyzer.Analyze(Path.GetFullPath(filePaths[index]), targets, _requestedTargetId));
            DatabaseStoreItem initialComponent = GetInitialComponent(analyzed.Result.Target.ComponentId);

            bool queued = DispatcherQueue.TryEnqueue(() =>
            {
                if (_isUnloaded)
                {
                    analyzed.Result.SharedPackage?.Dispose();
                    return;
                }
                _results.Add(new ConfigImportPresetViewModel(
                analyzed.Result,
                _components,
                initialComponent,
                analyzed.TargetWasDetected));
            });
            if (!queued)
                analyzed.Result.SharedPackage?.Dispose();
        }
    }

    private async Task PrepareSharedComponentsAsync()
    {
        var shared = _results.Where(item => item.Result.SharedPackage != null).ToArray();
        if (shared.Length == 0)
            return;
        var selections = _results.ToDictionary(item => item, item => item.SelectedComponent?.Id);
        var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in shared)
        {
            if (_isUnloaded)
                return;
            string missingId = ConfigShareService.GetMissingComponentId(item.Result.Config);
            if (missingId == null || !offered.Add(missingId))
                continue;
            try
            { await ConfigShareUI.OfferComponentInstallAsync(XamlRoot, item.Result.Config); }
            catch (Exception exception)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(MainPage), exception.ToString());
                if (!_isUnloaded)
                    item.SetOperationError(ConfigShareUI.ErrorText(exception));
            }
        }
        if (_isUnloaded)
            return;
        LoadComponents();
        foreach (var item in _results)
        {
            string selectedId = selections[item];
            if (selectedId == null && item.Result.SharedPackage != null)
                selectedId = ConfigShareService.GetInstalledComponentId(item.Result.Config);
            item.SelectedComponent = _components.FirstOrDefault(component => component.Id == selectedId);
        }
    }

    private async Task PrepareMissingFilesAsync()
    {
        foreach (ConfigImportPresetViewModel item in _results.Where(item => item.Result.IsSuccessful))
        {
            foreach (string missingPath in item.Result.MissingReferencedFiles)
            {
                bool suggestEmptyFile = _autoCorrector.ShouldSuggestEmptyFile(missingPath);
                string suggestion = suggestEmptyFile
                    ? null
                    : await Task.Run(() => _autoCorrector.FindReplacement(item.Result, missingPath));
                _missingFiles.Add(new ConfigImportMissingFileViewModel(
                    item,
                    missingPath,
                    suggestion,
                    suggestEmptyFile));

            }
        }
    }

    private DatabaseStoreItem GetInitialComponent(string conversionTargetId)
    {
        DatabaseStoreItem requested = _components.FirstOrDefault(item =>
            string.Equals(item.Id, _requestedTargetId, StringComparison.OrdinalIgnoreCase));
        if (requested != null)
            return requested;

        return _components.FirstOrDefault(item =>
            string.Equals(item.Id, conversionTargetId, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowCompletion()
    {
        UtilityButtons.IsLoading = false;
        NextButton.IsEnabled = true;

        NextButton.Content = _localizer.GetLocalizedString("SaveAll");

        MainContent.GoTo(CompletedStep);

        int successful = _results.Count(item => item.Result.IsSuccessful);
        if (successful == _results.Count)
        {
            CompletionInfoBar.Severity = InfoBarSeverity.Success;
            CompletionInfoBar.Title = _localizer.GetLocalizedString("ConfigImportCompletedSuccessTitle");
            CompletionInfoBar.Message = _localizer.GetLocalizedString("ConfigImportCompletedSuccessMessage");
        }
        else if (successful > 0)
        {
            CompletionInfoBar.Severity = InfoBarSeverity.Warning;
            CompletionInfoBar.Title = _localizer.GetLocalizedString("ConfigImportCompletedPartialTitle");
            CompletionInfoBar.Message = string.Format(
                _localizer.GetLocalizedString("ConfigImportCompletedPartialMessage"),
                successful,
                _results.Count);
        }
        else
        {
            CompletionInfoBar.Severity = InfoBarSeverity.Error;
            CompletionInfoBar.Title = _localizer.GetLocalizedString("ConfigImportCompletedFailedTitle");
            CompletionInfoBar.Message = _localizer.GetLocalizedString("ConfigImportCompletedFailedMessage");
        }
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton { Tag: ConfigImportPresetViewModel item } button)
            return;

        var detailsPanel = new StackPanel { Spacing = 10, Width = 350 };

        string titleText = item.HasDetails
            ? string.Format(_localizer.GetLocalizedString("ConfigImportErrorView"), item.FileName)
            : _localizer.GetLocalizedString("ConfigImportSuccess");

        detailsPanel.Children.Add(new TextBlock
        {
            Text = titleText,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (ConfigImportIssue issue in item.Result.Issues)
        {
            string location = issue.LineNumber.HasValue
                ? string.Format(_localizer.GetLocalizedString("ConfigImportIssueLine"), issue.LineNumber.Value)
                : string.Empty;
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"[{issue.Code}]{location} {ConfigImportIssueText.GetMessage(issue)}",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(item.OperationError))
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = item.OperationError,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = detailsPanel,
            },
        };
        flyout.ShowAt(button);
    }
}
