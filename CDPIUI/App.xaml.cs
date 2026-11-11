using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CDPIUI.Commands;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Helper.Basic;
using CDPIUI.Helper.Database;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Shared.Migration;
using Microsoft.UI.Xaml;
using WinUI3Localizer;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace CDPIUI;

public partial class App : Application
{
    public ElementTheme CurrentTheme { get; set; } = ElementTheme.Default;

    public List<Window> OpenWindows { get; private set; } = new List<Window>();

    private readonly ModalWindowCoordinator _modalWindows;
    private readonly AppWindowCoordinator<Window> _windowCoordinator;
    private readonly AppWindowCloseCoordinator<Window> _windowCloseCoordinator;
    private readonly AppActivationDispatcher _activationDispatcher;
    private readonly AppFileActivationHandler _fileActivationHandler;
    private readonly AppIdleExitController _idleExitController;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiDispatcher =
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    public Microsoft.UI.Dispatching.DispatcherQueue UiDispatcher => _uiDispatcher;

    public App()
    {
        _modalWindows = new ModalWindowCoordinator(OpenWindows);
        _windowCoordinator = new AppWindowCoordinator<Window>(OpenWindows, () => _localizerReady,
            new AppWindowOperations(RegisterWindow));
        _fileActivationHandler = new AppFileActivationHandler(
            new AppFileActivationOperations(this, OpenConditionalTaskImportAsync));
        _activationDispatcher = new AppActivationDispatcher(
            TryHandleMigrationActivationAsync,
            TryHandleMigrationActivationAsync,
            ProcessFiles,
            CommandsHandler.HandleCommandAsync,
            HandleCoreDirectActivationAsync,
            CommandsHandler.HandleCommand,
            CommandLineSplitter.Split,
            exception => Logger.Instance.CreateWarningLog(nameof(App), $"Cannot process startup activation: {exception}"));
        _idleExitController = new AppIdleExitController(
            () => OpenWindows.Count,
            () => Program.IsMigrationActivationPending,
            () => ApplicationTaskMonitor.IsStoreWorking(),
            () => ApplicationTaskMonitor.IsGoodCheckRunning(),
            () => SafeCreateNewWindow<PrepareWindow>(activate: false),
            () => GetCurrentWindowFromType<PrepareWindow>()?.Close(),
            Exit,
            exception => Logger.Instance.CreateWarningLog(
                nameof(App), $"Cannot update background operation state: {exception.Message}"));
        _windowCloseCoordinator = new AppWindowCloseCoordinator<Window>(OpenWindows,
            new AppWindowCloseOperations(_modalWindows,
                window => { window.Closed -= Window_ClosedHandler; window.SizeChanged -= Window_SizeChanged; },
                () => SafeCreateNewWindow<PrepareWindow>(activate: false), ExitIfIdle));
        this.InitializeComponent();
        RegisterUnhandledExceptionHandlers();
        PipeClientService.Instance.MessageReceived += (m) => CommandsHandler.HandleCommand(m);
        CoreErrorsLookupService.Instance.Init();

        UpdateThemeForAllWindows(EmergencyRecovery.Read(
            () => GetThemeFromString(SettingsManager.Instance.GetValue<string>("APPEARANCE", "Theme")), ElementTheme.Default));

        PipeClientService.Instance.Init();
        PipeClientService.Instance.Connected += PipeConnected;

        ApplicationTaskMonitor.Instance.StoreStateChanged += StoreStateChanged;

        GetReadyFeatures();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        string directArgs = arguments.FirstOrDefault(x => x.StartsWith("--direct:"));

        if (GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out _) ||
            GoodbyeDpiMigrationActivation.TryFindArgument(args.Arguments, out _))
        {
            Program.MarkMigrationActivationPending();
        }

        if (!Program.IsMigrationActivationPending)
        {
            _ = SafeCreateNewWindow<PrepareWindow>(string.IsNullOrEmpty(directArgs));
        }

        _ = PipeClientService.Instance.Start();
    }

    private void StoreStateChanged(bool isWorking) => _idleExitController.HandleStoreStateChanged(isWorking);

    private void PipeConnected()
    {
        SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "procState");
        SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide");
        SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "appUpdates");
        SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "storeUpdates");
        SettingsManager.Instance.GetValueOrDefault<bool>(
            "NOTIFICATIONS",
            "conditionalLaunchActions",
            defaultValue: true);

        _ = PipeConnectedActions();

        PipeClientService.Instance.Connected -= PipeConnected;
    }

    // Windows read their strings from the localizer, so they are created only after it is built.
    private readonly StartupInitialization _startupInitialization = new();
    private Task _localizerReady => _startupInitialization.Ready;

    public void GetReadyFeatures()
    {
        _startupInitialization.Start(GetReadyFeaturesAsync);
    }

    private async Task GetReadyFeaturesAsync()
    {
        try
        {
            DatabaseInitializationService.QuickRestore();
            await AppLocalizerInitializer.InitializeAsync();
            ApplicationInfo.Instance.SetLocalization(Localizer.Get().GetCurrentLanguage());
            FileAssociationService.EnsureRegistered();
        }
        catch (Exception ex)
        {
            EmergencyRecovery.BestEffort(() => Logger.Instance.CreateErrorLog(nameof(App), $"Cannot prepare the application: {ex}"));
            // Resource/settings failures must have visible text before regular windows can open.
            ErrorShowingHelper.RaiseCriticalException(nameof(GetReadyFeaturesAsync), ex);
            throw;
        }
    }

    private void ExitIfIdle() => _idleExitController.ExitIfIdle();

    public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
    {
        if (!typeof(TEnum).GetTypeInfo().IsEnum)
        {
            throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
        }
        return (TEnum)Enum.Parse(typeof(TEnum), text);
    }
}
