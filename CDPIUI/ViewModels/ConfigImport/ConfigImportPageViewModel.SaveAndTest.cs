using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.Views.ConfigImportUtil;

namespace CDPIUI.ViewModels.ConfigImport;

public sealed partial class ConfigImportPageViewModel
{
    private ConfigImportPresetViewModel _activeTestItem;
    private string _activeTestComponentId = string.Empty;
    private ConfigImportTestPlaceholder _activeTestPlaceholder;

    /// <summary>Saves one converted preset; the last saved preset ends the wizard.</summary>
    public async Task SaveAsync(ConfigImportPresetViewModel item)
    {
        if (item.SelectedComponent == null || !item.Result.IsSuccessful)
            return;

        if (await SaveItemAsync(item))
            Results.Remove(item);

        if (Results.Count == 0)
        {
            IsNextVisible = false;
            GoToStep(ConfigImportStep.WorkDone);
        }
    }

    private async Task<bool> SaveItemAsync(ConfigImportPresetViewModel item)
    {
        if (ReferenceEquals(_activeTestItem, item))
            await StopActiveTestAsync();

        item.SetSaving(true);
        if (item.Result.SharedPackage != null)
            _packagesBeingSaved.Add(item.Result.SharedPackage);
        try
        {
            ConfigImportResult selectedResult = GetSelectedResult(item);
            EnsureSuccessfulRetarget(selectedResult);
            ConfigImportInstallResult installResult = await _installer.InstallAsync(selectedResult, item.Name);
            if (!installResult.IsSuccessful)
            {
                item.SetOperationError(ConfigImportIssueText.GetInstallError(installResult.ErrorCode));
                return false;
            }

            ComponentItemsLoaderHelper.Instance
                .GetComponentHelperFromId(item.SelectedComponent.Id!)
                ?.ReInitConfigs();
            item.SetSaved();
            item.Result.SharedPackage?.Dispose();
            return true;
        }
        catch (Exception exception)
        {
            item.SetOperationError(exception.Message);
            return false;
        }
        finally
        {
            if (item.Result.SharedPackage != null)
            {
                _packagesBeingSaved.Remove(item.Result.SharedPackage);
                if (_isUnloaded)
                    item.Result.SharedPackage.Dispose();
            }
        }
    }

    /// <summary>Starts the component with the converted preset, or stops the running test of this preset.</summary>
    public async Task ToggleTestAsync(ConfigImportPresetViewModel item)
    {
        if (item.SelectedComponent == null || !item.Result.IsSuccessful)
            return;

        if (ReferenceEquals(_activeTestItem, item))
        {
            await StopActiveTestAsync();
            return;
        }

        await StopActiveTestAsync();
        try
        {
            ConfigImportResult selectedResult = GetSelectedResult(item);
            EnsureSuccessfulRetarget(selectedResult);
            selectedResult.Config!.packId = Path.GetDirectoryName(item.SourcePath)!;
            string arguments = ConfigurationService.GetStartupParametersByConfigItem(selectedResult.Config);
            if (string.IsNullOrWhiteSpace(arguments))
                throw new InvalidOperationException(_localizer.GetLocalizedString("ConfigImportTestEmptyArguments"));

            _activeTestPlaceholder = ConfigImportTestPlaceholder.Create(
                arguments,
                selectedResult.SourcePath,
                selectedResult.MissingReferencedFiles,
                selectedResult.MissingFileResolutions,
                selectedResult.GeneratedFiles);
            arguments = _activeTestPlaceholder.Arguments;

            _activeTestItem = item;
            _activeTestComponentId = item.SelectedComponent.Id!;
            await ComponentTasksManager.Instance.StopTask(_activeTestComponentId);
            item.SetTesting(true);
            await ComponentTasksManager.Instance.CreateAndRunNewTask(_activeTestComponentId, arguments);
            ComponentTasksManager.Instance.TaskStateUpdated += ComponentTasksManager_TaskStateUpdated;
            if (!await ComponentTasksManager.Instance.IsTaskRunning(_activeTestComponentId))
                throw new InvalidOperationException(_localizer.GetLocalizedString("ConfigImportTestProcessExited"));
        }
        catch (Exception exception)
        {
            await StopActiveTestAsync();
            item.SetOperationError(exception.Message);
        }
    }

    private ConfigImportResult GetSelectedResult(ConfigImportPresetViewModel item)
    {
        ConfigImportResult resolved = _importService.ApplyMissingFileResolutions(
            item.Result,
            item.MissingFileResolutions);
        return _importService.Retarget(resolved, ConfigImportTargetFactory.Create(item.SelectedComponent!));
    }

    private void EnsureSuccessfulRetarget(ConfigImportResult result)
    {
        if (result.IsSuccessful && result.Config != null)
            return;

        string errors = string.Join(
            Environment.NewLine,
            result.Issues
                .Where(issue => issue.Severity == ConfigImportIssueSeverity.Error)
                .Select(issue => ConfigImportIssueText.GetMessage(issue)));
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(errors)
            ? _localizer.GetLocalizedString("ConfigImportConvertFailed")
            : errors);
    }

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!string.Equals(state.Item1, _activeTestComponentId, StringComparison.OrdinalIgnoreCase) || state.Item2)
            return;

        _dispatcherQueue.TryEnqueue(CompleteTestSession);
    }

    private async Task StopActiveTestAsync()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        string componentId = _activeTestComponentId;
        if (!string.IsNullOrWhiteSpace(componentId))
        {
            try
            {
                await ComponentTasksManager.Instance.StopTask(componentId);
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
            }
        }
        CompleteTestSession();
    }

    private void CompleteTestSession()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        _activeTestItem?.SetTesting(false);
        _activeTestPlaceholder?.Dispose();
        _activeTestPlaceholder = null;
        _activeTestItem = null;
        _activeTestComponentId = string.Empty;
    }
}
