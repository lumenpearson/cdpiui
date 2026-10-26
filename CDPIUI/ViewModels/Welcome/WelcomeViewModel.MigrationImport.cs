using System;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Helper.Migration;

namespace CDPIUI.ViewModels.Welcome;

public sealed partial class WelcomeViewModel
{
    private async Task BeginMigrationImportAsync()
    {
        if (_migrationSession?.Package == null || _migrationImportInProgress || _migrationReadyToContinue)
            return;
        _migrationImportInProgress = true;
        DisconnectMigrationQueueHandlers();
        IsRetryMigrationVisible = false;
        IsMigrationComponentsListVisible = _migrationComponents.Count != 0;
        await _migrationSession.UpdateAsync(MigrationSessionState.ReadyToImport, 65);
        await _migrationSession.UpdateAsync(MigrationSessionState.Importing, 67);

        try
        {
            Progress<double> importProgress = new(value =>
            {
                double overall = 67 + Math.Clamp(value, 0, 100) * 0.31;
                MigrationProgress = overall;
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

            MigrationSummaryText = BuildMigrationImportSummary(result);
            _migrationReadyToContinue = true;
            _migrationImportInProgress = false;
            await _migrationSession.UpdateAsync(
                MigrationSessionState.Completed,
                100,
                result.Issues.Count > 0 ? Localized("MigrationImportIncompleteMessage") : null,
                result.Issues.Count > 0 ? "MIGRATION_IMPORT_INCOMPLETE" : null);
            _view.RefreshNavigation();
        }
        catch (OperationCanceledException)
        {
            _migrationImportInProgress = false;
            await FailMigrationAsync(Localized("MigrationCanceled"), "MIGRATION_CANCELED");
        }
        catch (Exception exception)
        {
            _migrationImportInProgress = false;
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
            MigrationSummaryText = Localized("MigrationImportIncompleteMessage");
            _migrationReadyToContinue = true;
            await _migrationSession.UpdateAsync(
                MigrationSessionState.Completed,
                100,
                Localized("MigrationImportIncompleteMessage"),
                "MIGRATION_IMPORT_INCOMPLETE");
            _view.RefreshNavigation();
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
            Logger.Instance.CreateWarningLog(LogSource, exception.ToString());
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
