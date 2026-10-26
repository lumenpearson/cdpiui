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
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiDispatcher =
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    public Microsoft.UI.Dispatching.DispatcherQueue UiDispatcher => _uiDispatcher;

    public App()
    {
        _modalWindows = new ModalWindowCoordinator(OpenWindows);
        this.InitializeComponent();
        RegisterUnhandledExceptionHandlers();
        PipeClientService.Instance.MessageReceived += (m) => CommandsHandler.HandleCommand(m);
        CoreErrorsLookupService.Instance.Init();

        UpdateThemeForAllWindows(GetThemeFromString(SettingsManager.Instance.GetValue<string>("APPEARANCE", "Theme")));

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

    private void StoreStateChanged(bool isWorking)
    {
        try
        {
            if (isWorking && OpenWindows.Count == 0 &&
                !Program.IsMigrationActivationPending)
            {
                _ = SafeCreateNewWindow<PrepareWindow>(activate: false);
            }
            if (!isWorking)
            {
                GetCurrentWindowFromType<PrepareWindow>()?.Close();
                ExitIfIdle();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateWarningLog(
                nameof(App),
                $"Cannot update background operation state: {ex.Message}");
            ExitIfIdle();
        }
    }

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
    private Task _localizerReady = Task.CompletedTask;

    public void GetReadyFeatures()
    {
        _localizerReady = GetReadyFeaturesAsync();
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
            Logger.Instance.CreateErrorLog(nameof(App), $"Cannot prepare the application: {ex}");
        }
    }

    private void ExitIfIdle()
    {
        if (Program.IsMigrationActivationPending && OpenWindows.Count == 0)
        {
            return;
        }
        if (OpenWindows.Count != 0 ||
            ApplicationTaskMonitor.IsStoreWorking() ||
            ApplicationTaskMonitor.IsGoodCheckRunning())
        {
            return;
        }

        Exit();
    }

    public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
    {
        if (!typeof(TEnum).GetTypeInfo().IsEnum)
        {
            throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
        }
        return (TEnum)Enum.Parse(typeof(TEnum), text);
    }
}
