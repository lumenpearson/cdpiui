using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Controls.Store;
using CDPIUI.Controls.Universal;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.System;
using CDPIUI.Default;
using CDPIUI.Extensions;
using CDPIUI.Helper;
using CDPIUI.Helper.Migration;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.ViewModels;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;


namespace CDPIUI;

public sealed partial class WelcomeWindow
{
    private async Task BeginMigrationImportAsync()
    {
        if (_migrationSession?.Package == null || _migrationImportInProgress || _migrationReadyToContinue)
            return;
        _migrationImportInProgress = true;
        DisconnectMigrationQueueHandlers();
        RetryMigrationButton.Visibility = Visibility.Collapsed;
        MigrationComponentsListView.Visibility = _migrationComponents.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        await _migrationSession.UpdateAsync(MigrationSessionState.ReadyToImport, 65);
        await _migrationSession.UpdateAsync(MigrationSessionState.Importing, 67);

        try
        {
            Progress<double> importProgress = new(value =>
            {
                double overall = 67 + Math.Clamp(value, 0, 100) * 0.31;
                MigrationProgressBar.Value = overall;
                _ = _migrationSession.UpdateAsync(MigrationSessionState.Importing, overall);
            });
            // Read before Task.Run: closing the window or a new session replaces the source.
            CancellationToken migrationToken = _migrationCancellation.Token;
            MigrationImportResult result = await Task.Run(() =>
                new MigrationImportService().Import(
                    _migrationSession.Package,
                    importProgress,
                    migrationToken));

            ReloadMigratedComponentConfigs(_migrationSession.Package, result);

            MigrationSummaryTextBlock.Text = BuildMigrationImportSummary(result);
            _migrationReadyToContinue = true;
            _migrationImportInProgress = false;
            await _migrationSession.UpdateAsync(
                MigrationSessionState.Completed,
                100,
                result.Issues.Count > 0 ? Localized("MigrationImportIncompleteMessage") : null,
                result.Issues.Count > 0 ? "MIGRATION_IMPORT_INCOMPLETE" : null);
            CheckNavigation();
        }
        catch (OperationCanceledException)
        {
            _migrationImportInProgress = false;
            await FailMigrationAsync(Localized("MigrationCanceled"), "MIGRATION_CANCELED");
        }
        catch (Exception exception)
        {
            _migrationImportInProgress = false;
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
            MigrationSummaryTextBlock.Text = Localized("MigrationImportIncompleteMessage");
            _migrationReadyToContinue = true;
            await _migrationSession.UpdateAsync(
                MigrationSessionState.Completed,
                100,
                Localized("MigrationImportIncompleteMessage"),
                "MIGRATION_IMPORT_INCOMPLETE");
            CheckNavigation();
        }
    }

    private static void ReloadMigratedComponentConfigs(
        VerifiedMigrationPackage package,
        MigrationImportResult result)
    {
        try
        {
            ComponentItemsLoaderHelper.Instance.Init();
            foreach (MigrationComponentRequirement component in package.Components)
                ComponentItemsLoaderHelper.Instance
                    .GetComponentHelperFromId(component.InstallItemId)?
                    .ReInitConfigs();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(WelcomeWindow), exception.ToString());
            result.Issues.Add(new MigrationImportIssue(
                "configuration-cache",
                "CDPIUI",
                exception.Message));
        }
    }

    private string BuildMigrationImportSummary(MigrationImportResult result)
    {
        string summary = string.Format(
            Localized("MigrationImportSummary"),
            result.ImportedPresetCount,
            result.ImportedResourceCount,
            result.ImportedSettingCount,
            result.ReviewRequiredCount);
        if (result.Issues.Count > 0)
        {
            summary += "\n\n" + Localized("MigrationImportIncompleteMessage") + " " +
                string.Format(Localized("MigrationImportWarningsSummary"), result.Issues.Count);
        }
        return summary;
    }
}
