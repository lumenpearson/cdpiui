using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Shared.Migration;
using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.Communication;

public class PipeClientService : PipeServiceBase
{
    private static PipeClientService? _instance;
    private static readonly object SyncRoot = new object();

    public static PipeClientService Instance
    {
        get
        {
            lock (SyncRoot)
            {
                _instance ??= new PipeClientService();
                return _instance;
            }
        }
    }

    public event Action<IPipeMessage>? MessageReceived;

    private bool _isInitialized;

    // The key of the tray start that published the pipe name of this connection.
    private string _sessionKey = string.Empty;

    private PipeClientService()
    {
        SettingsManager.Instance.EnumPropertyChanged += SettingsManager_EnumPropertyChanged;

        Logger = Basic.Logger.Instance;
    }

    private void SettingsManager_EnumPropertyChanged(IEnumerable<string> _enum)
    {
        if (_enum.Count() >= 2)
        {
            if (_enum.ElementAt(0) == "CONFIGS")
            {
                _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdStartupArgsChanged, _enum.ElementAt(1));
                _ = ComponentTasksManager.Instance.SendTaskData(_enum.ElementAt(1));
            }
        }
    }

    // The tray makes a new pipe name at each start, so the pipe stream is made when the GUI connects.
    public void Init()
    {
        _isInitialized = true;
    }

    // Catches and logs its own errors after the check below, so a caller can start it without waiting for it.
    public Task Start()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Call PipeClientService.Instance.Init first.");
        }
        return StartConnectionAsync();
    }

    private async Task StartConnectionAsync()
    {
        try
        {
            await ConnectToTrayAsync();

            if (PipeStream is not NamedPipeClientStream pipeStream || !pipeStream.IsConnected)
            {
                StartTrayProcess();
                Logger?.CreateErrorLog(nameof(PipeClientService), "Connection timeout");


                Process.GetCurrentProcess().Kill();
                return;
            }

            if (!PipePeerVerifier.IsServerExpected(pipeStream, Path.Combine(Data.Directories.CurrentDirectory, "CDPIUI_TrayIcon.exe")))
            {
                Logger?.CreateErrorLog(nameof(PipeClientService), "Pipe server is not the application tray process. Connection closed.");
                Dispose();
                Process.GetCurrentProcess().Kill();
                return;
            }

            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
            CreateCancellationToken();

            await RunConnectionUntilClosedAsync();
        }
        catch (Exception ex)
        {
            Logger?.CreateErrorLog(nameof(PipeClientService), $"Cannot start the pipe client: {ex}");
        }
    }

    private async Task ConnectToTrayAsync()
    {
        CreateCancellationToken();

        CancellationTokenSource.CancelAfter(2000);
        try
        {
            await ConnectToPublishedPipeAsync(Timeout.InfiniteTimeSpan, CancellationToken ?? default);
        }
        catch (OperationCanceledException)
        {
            // Expected when the tray does not run yet; the checks below start it.
        }
        catch (Exception ex)
        {
            Logger?.CreateWarningLog(nameof(PipeClientService), $"Cannot connect to the tray: {ex.Message}");
        }

        if (!IsConnected && IsTrayProcessRunning())
        {
            try
            {
                await ConnectToPublishedPipeAsync(TimeSpan.FromMilliseconds(TrayStartupWaitMs), default);
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                Logger?.CreateErrorLog(nameof(PipeClientService), $"Tray process is running but did not accept the connection: {ex.Message}");
            }
        }
    }

    // The tray publishes a new pipe name at each start, and only after it has created the pipe.
    // Thus the name and the key are read again for each attempt: a tray that starts in the
    // meantime is found, and a name that a program took after its tray ended is left.
    private async Task ConnectToPublishedPipeAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string? failedPipeName = null;
        while (true)
        {
            TimeSpan attempt = SessionReadInterval;
            if (timeout != Timeout.InfiniteTimeSpan)
            {
                TimeSpan remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException("The tray did not accept the connection in time.");
                if (remaining < attempt)
                    attempt = remaining;
            }

            PipeSession? session = PipeSessionFile.TryRead(AppContext.BaseDirectory);
            if (session == null)
            {
                await Task.Delay(attempt, cancellationToken);
                continue;
            }

            var pipeStream = new NamedPipeClientStream(".", session.PipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification);
            try
            {
                await pipeStream.ConnectAsync((int)Math.Ceiling(attempt.TotalMilliseconds), cancellationToken);
                _sessionKey = session.Key;
                PipeStream = pipeStream;
                return;
            }
            catch (TimeoutException)
            {
                pipeStream.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                pipeStream.Dispose();
                // Once for each name. The name is not logged: only the desktop user may know it.
                if (failedPipeName != session.PipeName)
                {
                    Logger?.CreateWarningLog(nameof(PipeClientService), $"Cannot connect to the published pipe: {ex.Message}");
                    failedPipeName = session.PipeName;
                }
                await Task.Delay(attempt, cancellationToken);
            }
            catch
            {
                pipeStream.Dispose();
                throw;
            }
        }
    }

    private void StartTrayProcess()
    {
        try
        {
            // The launcher removes the .NET variables of the user profile before it starts the tray.
            var psi = new ProcessStartInfo(
                ElevatedLauncherCommand.GetPath(Data.Directories.CurrentDirectory),
                ElevatedLauncherCommand.GetArguments(ElevatedLauncherCommand.TrayTarget, GetTrayStartupArguments()))
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            // For example, the user declined the elevation prompt.
            Logger?.CreateErrorLog(nameof(PipeClientService), $"Cannot start the tray: {ex.Message}");
        }
    }

    private static string GetTrayStartupArguments()
    {
        string startupString = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "hideToTrayOnStartup") ? "--autorun" : "--show-ui";
        if (GoodbyeDpiMigrationActivation.TryFindArgument(
            Environment.GetCommandLineArgs(), out var migrationRequest))
        {
            startupString = $"--show-ui {migrationRequest!.RawArgument}";
        }
        else if (Shared.Basic.CommandLine.GuiActivationArguments.Encode(
            Environment.GetCommandLineArgs().Skip(1)) is string guiArgs)
        {
            // The tray starts the GUI again with these, so the opened file or link is not lost.
            startupString = $"--show-ui {guiArgs}";
        }
        return startupString;
    }

    private async Task RunConnectionUntilClosedAsync()
    {
        try
        {
            await HandleConnectionAsync();
        }
        catch (Exception ex)
        {
            Logger?.CreateErrorLog(nameof(PipeClientService), $"The pipe connection failed: {ex}");
        }
        finally
        {
            CoreEvents.Instance.InvokeCriticalCoreExceptionHappens(new ErrorModel()
            {
                ErrorCode = "ERR_PROCESS_DIED",
                FriendlyDescription = "One of application process is died by unknown reason. Application can't restart this process, " +
                                      "because ERR_ACCESS_DENIED happens. Please, restart app manually.",
                Object = nameof(PipeClientService)
            });
            Dispose();
        }
    }

    private const int TrayStartupWaitMs = 15000;

    private static readonly TimeSpan SessionReadInterval = TimeSpan.FromMilliseconds(500);

    private static bool IsTrayProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("CDPIUI_TrayIcon");
            bool running = processes.Length > 0;
            foreach (var process in processes)
                process.Dispose();
            return running;
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected override async Task SendConnectionPacket()
    {
        // The key comes from the same read of the session file as the pipe name, so both belong to
        // the tray start that created the pipe.
        await SendMessageAsync(ServiceMessageModel.RequestAuth(_sessionKey).ToString());
    }

    protected override async Task RunMessageActions(IPipeMessage model)
    {
        Debug.WriteLine("Message actions");
        await base.RunMessageActions(model);

        CoreCommandsHandler.HandleCommand(model);

        MessageReceived?.Invoke(model);
    }
}
