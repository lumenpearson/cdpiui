using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Helper.Basic;
using CDPIUI.Helper.Migration;
using CDPIUI.Messages;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.Shared.Migration;
using CDPIUI.Shared.Pipe.Models;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI;

public partial class App
{
    private static readonly string[] SupportedFilePaths =
        [".cdpisignedpack", ".cdpiconfigpack", ".cdpipatch", ".cdpitask", ".cdpiconfig"];

    private readonly SemaphoreSlim _migrationWindowActivationLock = new(1, 1);
    private bool _settingsRecoveryWarningShown;

    private async Task PipeConnectedActions()
    {
        _ = TasksManagerHelper.Instance;
        string[] arguments = Environment.GetCommandLineArgs();

        await PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);
        await PipeHelper.SendConditionalTasksReloadPacket();
        await PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.GetAllProcessStates);

        ComponentTasksManager.Instance.RequestComponentItemsInit();

        bool isActivationHandled = await ProcessStartupActivationAsync(arguments);

        if (!isActivationHandled && !Program.IsMigrationActivationPending)
            await OpenStartupWindowAsync();

        GetCurrentWindowFromType<PrepareWindow>()?.Close();
        await ShowSettingsRecoveryWarningAsync();
        ExitIfIdle();
        Logger.Instance.CreateDebugLog(nameof(App), $"Arguments {arguments}");

    }

    /// <summary>
    /// Handles the migration request, files, protocol links and --direct: commands the first
    /// instance was started with. Returns true when one of them opened something.
    /// </summary>
    private async Task<bool> ProcessStartupActivationAsync(string[] arguments)
    {
        bool isFileProcessed = false;
        bool isActionPreferred = false;
        bool isMigrationProcessed = false;

        try
        {
            isMigrationProcessed = await TryHandleMigrationActivationAsync(arguments);
            if (!isMigrationProcessed &&
                Program.InitialActivationArguments?.Kind is ExtendedActivationKind.Launch &&
                Program.InitialActivationArguments.Data is ILaunchActivatedEventArgs initialLaunchArgs)
            {
                isMigrationProcessed = await TryHandleMigrationActivationAsync(initialLaunchArgs.Arguments);
            }

            if (Program.InitialActivationArguments?.Kind is ExtendedActivationKind.File &&
                Program.InitialActivationArguments.Data is IFileActivatedEventArgs initialFileArgs)
            {
                isFileProcessed = await ProcessFiles(
                    initialFileArgs.Files.Select(file => file.Path).ToArray());
            }

            if (Program.InitialActivationArguments?.Kind is ExtendedActivationKind.Protocol &&
                Program.InitialActivationArguments.Data is IProtocolActivatedEventArgs initialProtocolArgs)
            {
                isActionPreferred = await CommandsHandler.HandleCommandAsync(
                    initialProtocolArgs.Uri.ToString());
            }

            if (!isFileProcessed)
                isFileProcessed = await ProcessFiles(arguments);

            string protocolArg = isActionPreferred
                ? null
                : arguments.FirstOrDefault(x => x.StartsWith("----ms-protocol:"));

            if (protocolArg != null)
            {
                string value = protocolArg["----ms-protocol:".Length..];
                isActionPreferred = await CommandsHandler.HandleCommandAsync(value);
            }

            string directArgs = arguments.FirstOrDefault(x => x.StartsWith("--direct:"));

            if (directArgs != null)
            {
                string value = directArgs["--direct:".Length..];
                isActionPreferred = await CoreCommandsHandler.HandleCommandAsync(
                    PipeModelConvertor.ConvertBack(value));

                if (!isActionPreferred)
                {
                    isActionPreferred = CommandsHandler.HandleCommand(value);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(
                nameof(App),
                $"Cannot process startup activation: {ex}");
        }

        return isFileProcessed || isActionPreferred || isMigrationProcessed;
    }

    private async Task<bool> ProcessFiles(string[] files)
    {
        foreach (string file in files)
        {
            var _file = file.Replace("\"", "");
            Logger.Instance.CreateDebugLog(nameof(App), $"Working on file {_file}");

            if (Path.Exists(_file) && SupportedFilePaths.Contains(
                Path.GetExtension(_file),
                StringComparer.OrdinalIgnoreCase))
            {
                if (CDPIUI.AddOns.ConfigShare.ConfigShareService.IsSupported(_file))
                {
                    var importDialog = await UnsafeCreateNewWindow<ConfigShareImportDialog>(activate: false);
                    ActivateWindow(importDialog);
                    await importDialog.SetFileAsync(_file);
                    return true;
                }

                if (Path.GetExtension(_file).Equals(".cdpitask", StringComparison.OrdinalIgnoreCase))
                {
                    await OpenConditionalTaskImportAsync(_file);
                    return true;
                }

                if (Path.GetExtension(_file).Equals(".cdpipatch", StringComparison.OrdinalIgnoreCase))
                {
                    var updateDialog = await SafeCreateNewWindow<ApplicationUpdateFileDialogWindow>(activate: false);
                    updateDialog.SetUpdateFilePath(_file);
                    ActivateWindow(updateDialog);
                    return true;
                }

                var window = await SafeCreateNewWindow<StoreLocalItemInstallingDialog>();
                // Not awaited: the import runs while the dialog is open. SetPackFilePath logs its own errors.
                _ = window.SetPackFilePath(_file);
                return true; // Only first file will be processed.
            }
        }
        return false;
    }

    private async Task OpenConditionalTaskImportAsync(string filePath)
    {
        var imported = ConditionalTaskFileService.Load(filePath);
        var tasksDirectory = ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
            SettingsManager.Instance.SettingsFilePath);
        if (ConditionalTaskFileService.LoadDirectory(tasksDirectory).Any(task =>
            string.Equals(task.Id, imported.Id, StringComparison.OrdinalIgnoreCase)))
        {
            imported.Id = Guid.NewGuid().ToString("D");
        }

        imported.FilePath = null;
        imported.IsEnabled = false;
        var editor = await UnsafeCreateNewWindow<ConditionalTaskEditorWindow>(
            activate: false,
            id: ConditionalTaskEditorWindow.WindowIdPrefix + imported.Id);
        editor.SetTask(imported, tasksDirectory, isImport: true);
        ActivateWindow(editor);
    }

    // Catches and logs its own errors, so a caller can start it without waiting for it.
    public async Task PreferRequestedActions(AppActivationArguments appActivationArguments)
    {
        bool result = false;
        try
        {
            if (appActivationArguments.Kind is ExtendedActivationKind.Protocol &&
                appActivationArguments.Data is IProtocolActivatedEventArgs protocolActivatedEventArgs)
            {
                string value = protocolActivatedEventArgs.Uri.ToString();
                result = await CommandsHandler.HandleCommandAsync(value);
            }

            if (appActivationArguments.Kind is ExtendedActivationKind.File &&
                appActivationArguments.Data is IFileActivatedEventArgs activatedFileArgs)
            {
                result = await ProcessFiles(
                    activatedFileArgs.Files.Select(file => file.Path).ToArray());
            }
            else if (appActivationArguments.Kind is ExtendedActivationKind.Launch &&
                appActivationArguments.Data is ILaunchActivatedEventArgs fileActivatedEventArgs)
            {
                result = await TryHandleMigrationActivationAsync(fileActivatedEventArgs.Arguments);
                string[] args = CommandLineSplitter.Split(fileActivatedEventArgs.Arguments);
                if (!result)
                {
                    // The same --direct: commands as in a first start.
                    string directArgs = args.FirstOrDefault(x => x.StartsWith("--direct:"));
                    if (directArgs != null)
                    {
                        string value = directArgs["--direct:".Length..];
                        result = await CoreCommandsHandler.HandleCommandAsync(PipeModelConvertor.ConvertBack(value))
                            || CommandsHandler.HandleCommand(value);
                    }
                }
                if (!result)
                    result = await ProcessFiles(args);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(
                nameof(App),
                $"Cannot process redirected activation: {ex}");
            result = Program.IsMigrationActivationPending;
        }

        try
        {
            if (!result && !Program.IsMigrationActivationPending)
                await OpenStartupWindowAsync();
            else
                ExitIfIdle();
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(
                nameof(App),
                $"Cannot finish redirected activation: {ex}");
        }
    }

    internal async Task<Window> OpenStartupWindowAsync(bool activate = true)
    {
        var welcome = OpenWindows.OfType<WelcomeWindow>().FirstOrDefault();
        if (welcome != null)
        {
            if (activate)
                ActivateWindow(welcome);
            return welcome;
        }

        if (!SettingsManager.Instance.GetValueOrDefault("WELCOMEWIZARD", "Shown", defaultValue: false))
            return await SafeCreateNewWindow<WelcomeWindow>(activate);

        return await SafeCreateNewWindow<ModernMainWindow>(activate);
    }

    private Task<bool> TryHandleMigrationActivationAsync(string? arguments)
    {
        if (!GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out var request))
            return Task.FromResult(false);
        Program.MarkMigrationActivationPending();
        return OpenMigrationWelcomeAsync(request!);
    }

    private Task<bool> TryHandleMigrationActivationAsync(IEnumerable<string> arguments)
    {
        if (!GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out var request))
            return Task.FromResult(false);
        Program.MarkMigrationActivationPending();
        return OpenMigrationWelcomeAsync(request!);
    }

    private async Task<bool> OpenMigrationWelcomeAsync(
        GoodbyeDpiMigrationActivationRequest request)
    {
        await _migrationWindowActivationLock.WaitAsync();
        try
        {
            if (!GoodbyeDpiMigrationCoordinator.Instance.TryAccept(request, out var session) ||
                session == null)
            {
                WelcomeWindow existingWindow = OpenWindows
                    .OfType<WelcomeWindow>()
                    .FirstOrDefault(window => string.Equals(
                        window.Id,
                        WelcomeWindow.MigrationWindowId,
                        StringComparison.OrdinalIgnoreCase));
                if (existingWindow != null)
                    ActivateWindow(existingWindow);
                return true;
            }

            WelcomeWindow window = await UnsafeCreateNewWindow<WelcomeWindow>(
                activate: false,
                id: WelcomeWindow.MigrationWindowId);
            window.SetMigrationSession(session);
            CloseWindow<ModernMainWindow>();
            CloseWindow<MainWindow>();
            CloseWindow<PrepareWindow>();
            ActivateWindow(window);
            return true;
        }
        finally
        {
            if (!OpenWindows.OfType<WelcomeWindow>().Any(window => window.Id == WelcomeWindow.MigrationWindowId))
                Program.CompleteMigrationActivation();
            _migrationWindowActivationLock.Release();
        }
    }

    private async Task ShowSettingsRecoveryWarningAsync()
    {
        var settings = SettingsManager.Instance;
        if (_settingsRecoveryWarningShown ||
            (!settings.HasUnrecoverableLoadError && !File.Exists(settings.RecoveryNoticePath)))
            return;
        var owner = OpenWindows.LastOrDefault();
        if (owner == null)
            return;
        _settingsRecoveryWarningShown = true;
        try
        {
            var dialog = new Windows.UI.Popups.MessageDialog(
                Localizer.Get().GetLocalizedString("SettingsRecoveryWarningMessage"),
                Localizer.Get().GetLocalizedString("SettingsRecoveryWarningTitle"));
            InitializeWithWindow.Initialize(dialog, WindowNative.GetWindowHandle(owner));
            await dialog.ShowAsync();
            try
            { File.Delete(settings.RecoveryNoticePath); }
            // Best-effort cleanup: a notice that stays only shows the warning once more.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception ex)
        {
            _settingsRecoveryWarningShown = false;
            Logger.Instance.CreateWarningLog(nameof(App), ex.ToString());
        }
    }
}
