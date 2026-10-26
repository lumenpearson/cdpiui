using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CDPIUI.Shared.ComponentsTask;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.TrayIcon.Helper;

public class ProcessManager : IProcessService
{
    public string Id { get; set; } = string.Empty;
    public string ProcessName { get; private set; } = string.Empty;

    private bool _useProxy = false;
    private string _proxyType = string.Empty;
    private string _proxiFyreDir = string.Empty;
    private string _ip = string.Empty;
    private string _port = string.Empty;

    private string? _executable;
    private string? Args;

    public event Action<Tuple<string, bool>>? ProcessStateChanged;

    public bool IsProcessRunning => _conPTYHelper?.processState ?? false;

    // The tray asked the GUI for the startup string (the component never ran, or its arguments
    // changed) and the start has not arrived yet. A GUI that never answers does not keep it set.
    private static readonly TimeSpan GuiStartTimeout = TimeSpan.FromSeconds(15);
    private long _guiStartRequestedTicks;
    // A stop came while the start was pending: the start the GUI sends next is dropped.
    private long _guiStartCancelledTicks;

    private static bool IsRecent(long ticks) =>
        ticks != 0 && DateTime.UtcNow.Ticks - ticks < GuiStartTimeout.Ticks;

    /// <summary>A process runs, a start waits for its turn, or a start was asked from the GUI.</summary>
    public bool IsRunningOrStartRequested =>
        (_conPTYHelper?.IsRunningOrStarting ?? false) || IsRecent(Interlocked.Read(ref _guiStartRequestedTicks));

    public bool IsErrorHappens => LastError != null;
    public ErrorModel? LastError { get; private set; }


    private ConPTYHelper _conPTYHelper;
    private ConPTYHelper _proxiFyreHelper;


    public ProcessManager()
    {
        _conPTYHelper = new ConPTYHelper();
        _conPTYHelper.ProcessExited += SendStopMessage;
        _conPTYHelper.OutputAdded += SendOutput;
        _conPTYHelper.ErrorHappens += ShowErrorMessage;
        _conPTYHelper.ProcessStateChanged += ChangeProcessState;

        _proxiFyreHelper = new ConPTYHelper("PROXIFYRE");
        _proxiFyreHelper.ProcessExited += PFSendStopMessage;
        _proxiFyreHelper.OutputAdded += PFSendOutput;
        _proxiFyreHelper.ErrorHappens += PFShowErrorMessage;
        _proxiFyreHelper.ProcessStateChanged += PFChangeProcessState;
    }

    public bool IsProcessInfoChanged = false;

    // The tray sends errors, output and process names to the GUI through the pipe, so these
    // events of the interface are never raised here.
    event Action<ErrorModel>? IProcessService.ErrorHappens { add { } remove { } }
    event Action<string>? IProcessService.OutputReceived { add { } remove { } }
    event Action<string>? IProcessService.ProcessNameChanged { add { } remove { } }
    // The tray shows no error windows; errors go to the GUI through the pipe.
    event Action<string>? IProcessService.ShowErrorMessageWindow { add { } remove { } }

    public void InitProxy(string path)
    {
        _proxiFyreDir = path;
    }

    public void CleanProxy()
    {
        _proxyType = string.Empty;
        _proxiFyreDir = string.Empty;
        _useProxy = false;
    }

    public bool IsProxyEnabled()
    {
        if (_proxyType == "AllSystem")
            return true;

        return _proxiFyreHelper.processState;
    }

    public async Task StartProxy(string proxyType, string ip, string port)
    {
        _ip = ip;
        _port = port;
        _proxyType = proxyType;
        _useProxy = !string.IsNullOrEmpty(_proxyType);
        Debug.WriteLine($"Proxy setup for {_proxyType}, {ip}, {port}");
        if (!string.IsNullOrEmpty(_proxyType))
        {
            if (_proxyType == "AllSystem")
            {
                StartSystemProxy(ip, port);
            }
            else if (_proxyType == "ProxiFyre")
            {
                _ = _proxiFyreHelper.RunProcess(_proxiFyreDir, string.Empty, Path.GetDirectoryName(_proxiFyreDir)!);
            }
        }
        await Task.CompletedTask;
    }

    // 1 while the system proxy set by this component is on.
    private int _systemProxyStarted;

    // Runs for the stop command and again for the exit events of the process: only the first
    // call after StartProxy restores the system proxy.
    public async Task StopProxy()
    {
        if (Interlocked.Exchange(ref _systemProxyStarted, 0) == 1)
            StopSystemProxy();

        if (_proxiFyreHelper.processState)
            await _proxiFyreHelper.StopProcess();
    }

    public async Task StartProcess()
    {
        if (_executable != null && Args != null && !IsProcessInfoChanged)
        {
            if (_useProxy)
                await StartProxy(_proxyType, _ip, _port);

            await StartProcess(_executable, Args);
        }
        else
        {
            Interlocked.Exchange(ref _guiStartCancelledTicks, 0);
            Interlocked.Exchange(ref _guiStartRequestedTicks, DateTime.UtcNow.Ticks);
            await PipeHelper.SendConPTYPacket(CONPTYMessageIds.GetStartupString, Id, openIfNotConnected: true);
        }
    }


    public async Task StartProcess(string executable, string? args)
    {
        Interlocked.Exchange(ref _guiStartRequestedTicks, 0);
        // The answer to a startup string request that a stop has cancelled meanwhile.
        if (IsRecent(Interlocked.Exchange(ref _guiStartCancelledTicks, 0)))
        {
            Logger.Instance.CreateInfoLog(nameof(ProcessManager), $"The start of {Id} was cancelled before the GUI sent its arguments.");
            return;
        }

        LastError = null;
        IsProcessInfoChanged = false;
        _executable = executable;
        Args = args;

        var exePath = executable;
        var workingDirectory = Path.GetDirectoryName(exePath);

        ProcessName = Path.GetFileName(exePath);
        await PipeHelper.SendConPTYPacket(CONPTYMessageIds.CleanOutputForId, Id);
        await PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStarted, Id);

        SendNowSelectedComponentName();

        _ = _conPTYHelper.RunProcess(exePath, args ?? string.Empty, workingDirectory ?? "");
    }

    public void SendNowSelectedComponentName()
    {
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ChangeProcessIdExecutable, Id, new()
        {
            {"processName", ProcessName }
        });
    }



    /// <summary>
    /// Cancels a start that waits for the startup string from the GUI: the start the GUI sends
    /// next is dropped. Returns false when no such start is pending.
    /// </summary>
    public bool CancelRequestedStart()
    {
        if (!IsRecent(Interlocked.Exchange(ref _guiStartRequestedTicks, 0)))
            return false;
        Interlocked.Exchange(ref _guiStartCancelledTicks, DateTime.UtcNow.Ticks);
        return true;
    }

    public async Task StopProcess(bool output = true)
    {
        // A start that still waits for its turn counts too: it ran after this stop when the
        // previous process had just exited.
        if (_conPTYHelper.IsRunningOrStarting)
        {
            await StopProxy();
            await _conPTYHelper.StopProcess(output);
        }

        await Task.CompletedTask;
    }

    public async Task RestartProcess()
    {
        await StopProcess();
        await Task.Delay(1000);
        await StartProcess();
    }

    public static async Task StopService()
    {
        string sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        foreach (string command in new[] { "stop WinDivert", "delete WinDivert" })
        {
            try
            {
                var (exitCode, output) = await ExecuteCommand(sc, command);
                // 1060: the service does not exist, 1062: it is not running. Other codes were
                // printed to a console the tray does not have.
                if (exitCode != 0 && exitCode != 1060 && exitCode != 1062)
                    Logger.Instance.CreateWarningLog(nameof(ProcessManager), $"sc {command} failed with {exitCode}: {output.Trim()}");
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ProcessManager), $"Cannot run sc {command}: {ex.Message}");
            }
        }
    }

    private static async Task<(int ExitCode, string Output)> ExecuteCommand(string fileName, string arguments)
    {
        using (Process process = new Process())
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            int lcid = GetSystemDefaultLCID();
            var ci = System.Globalization.CultureInfo.GetCultureInfo(lcid);
            var page = ci.TextInfo.OEMCodePage;

            process.StartInfo.FileName = fileName;
            process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(page);
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            // Both streams are read at once, so a full error pipe cannot block the process.
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string output = await outputTask;
            string error = await errorTask;

            return (process.ExitCode, string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}");
        }
    }

    public void SendDefaultProcessOutput()
    {
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdFullOutput, Id, new()
        {
            { "output", $"{_proxiFyreHelper.GetDefaultOutput()}\n{_conPTYHelper.GetDefaultOutput()}" }
        });
    }
    public void SendState()
    {
        if (LastError is { } error)
        {
            _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id, new()
            {
                { "errorHappens", "true" },
                { "errorMessage", error.ErrorCode },
                { "errorObject", error.Object },
                { "stateSnapshot", "true" },
            });
        }
        else if (_conPTYHelper.processState)
        {
            _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStarted, Id);
        }
        else
        {
            _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id);
        }
    }

    // The system proxy is global, so the user's previous settings are kept once for all components.
    private static readonly object SystemProxySyncRoot = new();
    private static RegeditHelper.ProxySnapshot? _systemProxyBackup;

    private void StartSystemProxy(string ip, string port)
    {
        string proxyServer = string.Empty;
        IPAddress address;
        if (IPAddress.TryParse(ip, out address))
        {
            proxyServer = address.AddressFamily switch
            {
                System.Net.Sockets.AddressFamily.InterNetwork => $"socks={ip}:{port}",
                System.Net.Sockets.AddressFamily.InterNetworkV6 => $"socks=[{ip}]:{port}",
                _ => "",
            };
        }

        if (string.IsNullOrEmpty(proxyServer))
        {
            ShowErrorMessage(Tuple.Create(PrettyErrorCode.IP_INCORRECT, nameof(StartSystemProxy)));
            return;
        }
        try
        {
            lock (SystemProxySyncRoot)
            {
                if (_systemProxyBackup == null)
                {
                    // Kept in memory only once the backup file is written, so a failed write
                    // does not leave a backup that a later start would reuse.
                    var snapshot = RegeditHelper.ReadProxySnapshot();
                    RegeditHelper.SaveSnapshotBackup(snapshot);
                    _systemProxyBackup = snapshot;
                }
                // Set before the write: SaveProxySettings can write the registry and then fail
                // to notify WinINet, and the following StopProxy must still restore the backup.
                Interlocked.Exchange(ref _systemProxyStarted, 1);
                RegeditHelper.SaveProxySettings(proxyServer, string.Empty, 1);
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage(Tuple.Create(ErrorHelper.Convertor.GetLoggedErrorCode(nameof(RegeditHelper), ex), nameof(RegeditHelper)));
            Logger.Instance.CreateErrorLog(nameof(ProcessManager), $"{ex.Message}");
        }
    }

    private void StopSystemProxy()
    {
        try
        {
            lock (SystemProxySyncRoot)
            {
                // Without a backup the settings were already restored; clearing them here
                // wiped the user's own proxy.
                if (_systemProxyBackup == null)
                    return;
                RegeditHelper.RestoreProxySnapshot(_systemProxyBackup);
                _systemProxyBackup = null;
                RegeditHelper.DeleteSnapshotBackup();
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage(Tuple.Create(ErrorHelper.Convertor.GetLoggedErrorCode(nameof(RegeditHelper), ex), nameof(RegeditHelper)));
            Logger.Instance.CreateErrorLog(nameof(ProcessManager), $"{ex.Message}");
        }
    }

    #region MessageHandler
    private void SendStopMessage(string output = "Process will be stopped by user")
    {
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdNewOutput, Id, new()
        {
            { "output", $"{output}" }
        });
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id);
        _ = StopProxy();
    }
    private void SendOutput(string output)
    {
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdNewOutput, Id, new()
        {
            { "output", $"{output}" }
        });
    }
    private void ChangeProcessState(bool isRunning)
    {
        ProcessStateChanged?.Invoke(Tuple.Create(Id, isRunning));

        Debug.WriteLine($"Process state is {isRunning}");
        if (isRunning == false)
        {
            _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id);
            _ = StopProxy();
        }
    }
    private void ShowErrorMessage(Tuple<PrettyErrorCode, string> tuple)
    {
        PrettyErrorCode message = tuple.Item1;
        string _object = tuple.Item2;
        LastError = new ErrorModel { ErrorCode = message.ToString(), Object = _object };
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id, new()
        {
            { "errorHappens", "true" },
            { "errorMessage", message.ToString() },
            { "errorObject", _object },
        });
        _ = StopProxy();
    }

    private void PFSendStopMessage(string output = "Process will be stopped by user")
    {
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdNewOutput, Id, new()
        {
            { "output", $"{output}" }
        });
        _ = _conPTYHelper.StopProcess();
    }
    private void PFSendOutput(string output)
    {
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdNewOutput, Id, new()
        {
            { "output", $"{output}" }
        });
    }
    private void PFChangeProcessState(bool isRunning)
    {
        ProcessStateChanged?.Invoke(Tuple.Create(Id, isRunning));
        if (isRunning == false)
            _ = _conPTYHelper.StopProcess();
    }
    private void PFShowErrorMessage(Tuple<PrettyErrorCode, string> tuple)
    {
        PrettyErrorCode message = tuple.Item1;
        string _object = tuple.Item2;
        LastError = new ErrorModel { ErrorCode = message.ToString(), Object = _object };
        _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id, new()
        {
            { "errorHappens", "true" },
            { "errorMessage", message.ToString() },
            { "errorObject", _object },
        });
        _ = _conPTYHelper.StopProcess();
    }

    #endregion

    #region WINAPI

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    public static extern int GetSystemDefaultLCID();

    #endregion
}
