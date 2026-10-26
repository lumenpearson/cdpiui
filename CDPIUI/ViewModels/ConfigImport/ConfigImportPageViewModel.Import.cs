using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Helper.AddOns.ConfigShare;
using CDPIUI.Views.ConfigImportUtil;

namespace CDPIUI.ViewModels.ConfigImport;

public sealed partial class ConfigImportPageViewModel
{
    /// <summary>Imports files dropped on the window, now or when the page loads.</summary>
    public void QueueDroppedFiles(string[] paths)
    {
        _pendingDroppedFiles = paths.ToArray();
        if (_view.IsViewLoaded)
            _ = ImportPendingFilesAsync();
    }

    private async Task ImportPendingFilesAsync()
    {
        if (_pendingDroppedFiles == null || _importingDroppedFiles || _isUnloaded)
            return;
        var paths = _pendingDroppedFiles;
        _pendingDroppedFiles = null;
        _importingDroppedFiles = true;
        IsNextVisible = false;
        try
        { await ImportConfigsAsync(paths); }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
            if (!_isUnloaded && _view.IsViewLoaded)
                await _view.ShowErrorAsync(exception.Message);
        }
        finally
        {
            _importingDroppedFiles = false;
            if (!_isUnloaded)
            {
                IsLoading = false;
                IsNextVisible = true;
            }
        }
    }

    private async Task<bool> ImportConfigsAsync(string[] suppliedPaths = null)
    {
        LoadComponents();
        if (Components.Count == 0)
            return false;
        string[] filePaths = suppliedPaths;
        if (filePaths == null)
        {
            filePaths = _view.PickImportFiles();
            if (filePaths == null)
                return false;
        }

        if (filePaths.Length == 0)
            return false;

        IsLoading = true;

        GoToStep(ConfigImportStep.Progress);

        foreach (var item in Results)
            item.Result.SharedPackage?.Dispose();
        Results.Clear();
        MissingFiles.Clear();

        await Task.Run(() => ImportWork(filePaths));
        if (_isUnloaded)
            return false;

        await PrepareSharedComponentsAsync();
        if (_isUnloaded)
            return false;

        await PrepareMissingFilesAsync();
        if (_isUnloaded)
            return false;

        if (MissingFiles.Count > 0)
            ShowMissingFiles();
        else
            ShowCompletion();

        return true;
    }

    private async Task ImportWork(string[] filePaths)
    {
        List<ConfigImportTarget> targets = [.. Components.Select(ConfigImportTargetFactory.Create)];
        for (int index = 0; index < filePaths.Length; index++)
        {

            string text = string.Format(
                _localizer.GetLocalizedString("ConfigImportProgressCounter"),
                index + 1,
                filePaths.Length,
                Path.GetFileName(filePaths[index]));

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!_isUnloaded)
                    LoadingStateText = text;
            });

            AnalyzedConfigImport analyzed = await Task.Run(() =>
                _fileAnalyzer.Analyze(Path.GetFullPath(filePaths[index]), targets, _requestedTargetId));
            DatabaseStoreItem initialComponent = GetInitialComponent(analyzed.Result.Target.ComponentId);

            bool queued = _dispatcherQueue.TryEnqueue(() =>
            {
                if (_isUnloaded)
                {
                    analyzed.Result.SharedPackage?.Dispose();
                    return;
                }
                Results.Add(new ConfigImportPresetViewModel(
                analyzed.Result,
                Components,
                initialComponent,
                analyzed.TargetWasDetected));
            });
            if (!queued)
                analyzed.Result.SharedPackage?.Dispose();
        }
    }

    private async Task PrepareSharedComponentsAsync()
    {
        var shared = Results.Where(item => item.Result.SharedPackage != null).ToArray();
        if (shared.Length == 0)
            return;
        var selections = Results.ToDictionary(item => item, item => item.SelectedComponent?.Id);
        var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in shared)
        {
            if (_isUnloaded)
                return;
            string missingId = ConfigShareService.GetMissingComponentId(item.Result.Config);
            if (missingId == null || !offered.Add(missingId))
                continue;
            try
            { await _view.OfferComponentInstallAsync(item.Result.Config); }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
                if (!_isUnloaded)
                    item.SetOperationError(ConfigShareUI.ErrorText(exception));
            }
        }
        if (_isUnloaded)
            return;
        LoadComponents();
        foreach (var item in Results)
        {
            string selectedId = selections[item];
            if (selectedId == null && item.Result.SharedPackage != null)
                selectedId = ConfigShareService.GetInstalledComponentId(item.Result.Config);
            item.SelectedComponent = Components.FirstOrDefault(component => component.Id == selectedId);
        }
    }

    private async Task PrepareMissingFilesAsync()
    {
        foreach (ConfigImportPresetViewModel item in Results.Where(item => item.Result.IsSuccessful))
        {
            foreach (string missingPath in item.Result.MissingReferencedFiles)
            {
                bool suggestEmptyFile = _autoCorrector.ShouldSuggestEmptyFile(missingPath);
                string suggestion = suggestEmptyFile
                    ? null
                    : await Task.Run(() => _autoCorrector.FindReplacement(item.Result, missingPath));
                MissingFiles.Add(new ConfigImportMissingFileViewModel(
                    item,
                    missingPath,
                    suggestion,
                    suggestEmptyFile));

            }
        }
    }

    private DatabaseStoreItem GetInitialComponent(string conversionTargetId)
    {
        DatabaseStoreItem requested = Components.FirstOrDefault(item =>
            string.Equals(item.Id, _requestedTargetId, StringComparison.OrdinalIgnoreCase));
        if (requested != null)
            return requested;

        return Components.FirstOrDefault(item =>
            string.Equals(item.Id, conversionTargetId, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowCompletion()
    {
        IsLoading = false;
        SetNextEnabled(true);

        NextButtonText = _localizer.GetLocalizedString("SaveAll");

        GoToStep(ConfigImportStep.Completed);

        int successful = Results.Count(item => item.Result.IsSuccessful);
        if (successful == Results.Count)
        {
            CompletionState = ConfigImportCompletionState.Success;
            CompletionTitle = _localizer.GetLocalizedString("ConfigImportCompletedSuccessTitle");
            CompletionMessage = _localizer.GetLocalizedString("ConfigImportCompletedSuccessMessage");
        }
        else if (successful > 0)
        {
            CompletionState = ConfigImportCompletionState.Partial;
            CompletionTitle = _localizer.GetLocalizedString("ConfigImportCompletedPartialTitle");
            CompletionMessage = string.Format(
                _localizer.GetLocalizedString("ConfigImportCompletedPartialMessage"),
                successful,
                Results.Count);
        }
        else
        {
            CompletionState = ConfigImportCompletionState.Failed;
            CompletionTitle = _localizer.GetLocalizedString("ConfigImportCompletedFailedTitle");
            CompletionMessage = _localizer.GetLocalizedString("ConfigImportCompletedFailedMessage");
        }
    }
}
