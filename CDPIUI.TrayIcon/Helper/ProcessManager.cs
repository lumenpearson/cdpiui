using CDPIUI.Shared.ComponentsTask;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CDPIUI.Shared.Extensions;
using CDPIUI.Shared.Pipe.Models;

namespace CDPIUI.TrayIcon.Helper
{
    public class ProcessManager : IProcessService
    {
        public string Id { get; set; } = string.Empty;
        public string ProcessName { get; private set; } = string.Empty;

        private bool useProxy = false;
        private string proxyType = string.Empty;
        private string proxiFyreDir = string.Empty;
        private string _ip = string.Empty;
        private string _port = string.Empty;

        private string? Executable;
        private string? Args;

        public event Action<Tuple<string, bool>>? ProcessStateChanged;
        
        public bool IsProcessRunning => conPTYHelper?.processState ?? false;

        public bool IsErrorHappens => LastError != null;
        public ErrorModel? LastError { get; private set; }


        private ConPTYHelper conPTYHelper;
        private ConPTYHelper proxiFyreHelper;
        

        public ProcessManager()
        {
            conPTYHelper = new ConPTYHelper();
            conPTYHelper.ProcessExited += SendStopMessage;
            conPTYHelper.OutputAdded += SendOutput;
            conPTYHelper.ErrorHappens += ShowErrorMessage;
            conPTYHelper.ProcessStateChanged += ChangeProcessState;

            proxiFyreHelper = new ConPTYHelper("PROXIFYRE");
            proxiFyreHelper.ProcessExited += PFSendStopMessage;
            proxiFyreHelper.OutputAdded += PFSendOutput;
            proxiFyreHelper.ErrorHappens += PFShowErrorMessage;
            proxiFyreHelper.ProcessStateChanged += PFChangeProcessState;
        }

        public bool IsProcessInfoChanged = false;

        public event Action<ErrorModel>? ErrorHappens;
        public event Action<string>? OutputReceived;
        public event Action<string>? ProcessNameChanged;
        // The tray shows no error windows; errors go to the GUI through the pipe.
        event Action<string>? IProcessService.ShowErrorMessageWindow { add { } remove { } }

        public void InitProxy(string path)
        {
            proxiFyreDir = path;
        }

        public void CleanProxy()
        {
            proxyType = string.Empty;
            proxiFyreDir = string.Empty;
            useProxy = false;   
        }

        public bool IsProxyEnabled()
        {
            if (proxyType == "AllSystem")
                return true;

            return proxiFyreHelper.processState;
        }

        public async Task StartProxy(string _proxyType, string ip, string port)
        {
            _ip = ip;
            _port = port;
            proxyType = _proxyType;
            useProxy = !string.IsNullOrEmpty(proxyType);
            Debug.WriteLine($"Proxy setup for {proxyType}, {ip}, {port}");
            if (!string.IsNullOrEmpty(proxyType))
            {
                if (proxyType == "AllSystem")
                {
                    StartSystemProxy(ip, port);
                }
                else if (proxyType == "ProxiFyre")
                {
                    proxiFyreHelper.RunProcess(proxiFyreDir, string.Empty, Path.GetDirectoryName(proxiFyreDir)!);
                }
            }
            await Task.CompletedTask;
        }

        public async Task StopProxy()
        {
            if (proxyType == "AllSystem")
                StopSystemProxy();

            if (proxiFyreHelper.processState)
                await proxiFyreHelper.StopProcess();
        }
        
        public async Task StartProcess()
        {
            if (Executable != null && Args != null && !IsProcessInfoChanged)
            {
                if (useProxy)
                    await StartProxy(proxyType, _ip, _port);

                await StartProcess(Executable, Args);
            }
            else
            {
                await PipeHelper.SendConPTYPacket(CONPTYMessageIds.GetStartupString, Id, openIfNotConnected: true);
            }
        }
        

        public async Task StartProcess(string executable, string args)
        {
            LastError = null;
            IsProcessInfoChanged = false;
            Executable = executable;
            Args = args;
            
            var exePath = executable;
            var workingDirectory = Path.GetDirectoryName(exePath);

            ProcessName = Path.GetFileName(exePath);
            await PipeHelper.SendConPTYPacket(CONPTYMessageIds.CleanOutputForId, Id);
            await PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStarted, Id);

            SendNowSelectedComponentName();

            conPTYHelper.RunProcess(exePath, args, workingDirectory?? "");
        }

        public void SendNowSelectedComponentName()
        {
            _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ChangeProcessIdExecutable, Id, new()
            { 
                {"processName", ProcessName }
            });
        }

       

        public async Task StopProcess(bool output = true)
        {
            if (conPTYHelper.processState)
            {
                await StopProxy();
                await conPTYHelper.StopProcess(output);
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
                { "output", $"{proxiFyreHelper.GetDefaultOutput()}\n{conPTYHelper.GetDefaultOutput()}" }
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
            else if (conPTYHelper.processState)
            {
                _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStarted, Id);
            }
            else
            {
                _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.MarkProcessIdAsStopped, Id);
            }
        }

        // The system proxy is global, so the user's previous settings are kept once for all components.
        private static readonly object SystemProxyLock = new();
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
                lock (SystemProxyLock)
                {
                    if (_systemProxyBackup == null)
                    {
                        _systemProxyBackup = RegeditHelper.ReadProxySnapshot();
                        RegeditHelper.SaveSnapshotBackup(_systemProxyBackup);
                    }
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
                lock (SystemProxyLock)
                {
                    if (_systemProxyBackup != null)
                        RegeditHelper.RestoreProxySnapshot(_systemProxyBackup);
                    else
                        RegeditHelper.SaveProxySettings(string.Empty, string.Empty, 0);
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
            ProcessStateChanged?.Invoke(Tuple.Create(Id,isRunning));

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
            _ = conPTYHelper.StopProcess();
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
            if (isRunning == false) _ = conPTYHelper.StopProcess();
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
            _ = conPTYHelper.StopProcess();
        }

        #endregion

        #region WINAPI

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();

        #endregion
    }
}
