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
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConfigImportPresetViewModel item } ||
            item.SelectedComponent == null ||
            !item.Result.IsSuccessful)
        {
            return;
        }
        if (await SaveItemAsync(item))
            _results.Remove(item);

        if (_results.Count == 0)
        {
            NextButton.Visibility = Visibility.Collapsed;
            MainContent.GoTo(WorkDoneStep);
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

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConfigImportPresetViewModel item } ||
            item.SelectedComponent == null ||
            !item.Result.IsSuccessful)
        {
            return;
        }

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

        DispatcherQueue.TryEnqueue(CompleteTestSession);
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
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(MainPage), exception.ToString());
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
