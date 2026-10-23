using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Data;
using CDPIUI.Core.Proxy;
using CDPIUI.Core.Store;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace CDPIUI.AddOns.Troubleshooting
{
    public class TroubleshootingService
    {
        private static TroubleshootingService? _instance;
        private static readonly object _lock = new object();

        public static TroubleshootingService Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new TroubleshootingService();
                    return _instance;
                }
            }
        }

        public Action<Enum>? CurrentStateChanged;

        private TroubleshootingService()
        {

        }

        public class DiagnosticStateModel
        {
            public string? CorrectValueDisplayText { get; set; }
            public string? InCorrectValueDisplayText { get; set; }
            public bool CorrectState { get; set; }
        }

        public enum StoreCheckStates
        {
            Preparing,
            CheckGitHubRepo,
            CheckGitLabRepo,
            CheckWriteAccess,
            CheckWriteAccessIntoItemsFolder,
            CheckWriteAccessIntoRepoFolder,
            CheckWriteAccessIntoDatabaseFolder,
            Completed
        }

        public static Dictionary<StoreCheckStates, DiagnosticStateModel> StoreDiagnosticStateModelPairs = new()
        {
            { StoreCheckStates.CheckGitHubRepo, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckGitLabRepo, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccess, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccessIntoItemsFolder, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccessIntoRepoFolder, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccessIntoDatabaseFolder, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
        };

        public async Task<Dictionary<StoreCheckStates, bool>> RunStoreDiagnostic()
        {
            await ComponentTasksManager.Instance.StopAllTasks();
            _ = ProcessService.StopService();
            CurrentStateChanged?.Invoke(StoreCheckStates.Preparing);

            await Task.Delay(1000);
            Services = null;
            Dictionary<StoreCheckStates, bool> completedStates = [];

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckGitHubRepo);
            bool isGitHubRepoAvailable = await StoreHelper.TryLoadDatabaseForVersionControl(Core.Store.Repository.SupportedVersionControls.GitHub);

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckGitLabRepo);
            bool isGitLabRepoAvailable = await StoreHelper.TryLoadDatabaseForVersionControl(Core.Store.Repository.SupportedVersionControls.GitLab);

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccess);
            string localAppData = Core.Data.Directories.DataDirectory;
            string storeFolder = Directories.StoreDirectory;

            bool hasWriteAccess = IsDirectoryWritable(Path.Combine(storeFolder));

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccessIntoItemsFolder);
            bool hasWriteAccessIntoItemsFolder = IsDirectoryWritable(Directories.StoreItemsDirectory);

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccessIntoRepoFolder);
            bool hasWriteAccessIntoRepoFolder = IsDirectoryWritable(Directories.StoreRepoCacheDirectory);

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccessIntoDatabaseFolder);
            bool hasWriteAccessIntoDatabaseFolder = IsDirectoryWritable(Directories.StoreLocalCacheDirectory);

            await Task.Delay(1000);

            completedStates = new()
            {
                { StoreCheckStates.CheckGitHubRepo, isGitHubRepoAvailable },
                { StoreCheckStates.CheckGitLabRepo, isGitLabRepoAvailable },
                { StoreCheckStates.CheckWriteAccess, hasWriteAccess },
                { StoreCheckStates.CheckWriteAccessIntoItemsFolder, hasWriteAccessIntoItemsFolder },
                { StoreCheckStates.CheckWriteAccessIntoRepoFolder, hasWriteAccessIntoRepoFolder },
                { StoreCheckStates.CheckWriteAccessIntoDatabaseFolder, hasWriteAccessIntoDatabaseFolder },
            };

            await Task.CompletedTask;
            Services = null;

            CurrentStateChanged?.Invoke(StoreCheckStates.Completed);

            return completedStates;
        }

        public async Task<Dictionary<StoreCheckStates, FixResultModel>> FixAllStoreErrors(Dictionary<StoreCheckStates, bool> statesList)
        {
            Dictionary<StoreCheckStates, FixResultModel> result = [];
            foreach (var state in statesList)
            {
                if (state.Value != (StoreDiagnosticStateModelPairs.FirstOrDefault(x => x.Key == state.Key).Value?.CorrectState ?? false))
                {
                    result.Add(state.Key, await TryToFixStoreError(state.Key));
                }
            }
            await Task.Delay(1000);
            return result;
        }

        public enum RunBasicDialogStates
        {
            Preparing,
            CheckBFE,
            CheckProxy,
            CheckNetsh,
            CheckTimestamps,
            CheckAdGuard,
            CheckKiller,
            CheckIntelConnectivityNetwork,
            CheckCheckPointServices,
            CheckSmartByte,
            CheckVPNs,
            CheckDNS,
            CheckWinDivert,
            CheckAnotherComponents,
            CheckAnotherComponentsServices,
            Completed
        }


        public static Dictionary<RunBasicDialogStates, DiagnosticStateModel> BasicDiagnosticStateModelPairs = new()
        {
            { RunBasicDialogStates.CheckBFE, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "ServiceRunning", InCorrectValueDisplayText="ServiceStopped" } },
            { RunBasicDialogStates.CheckProxy, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "Disabled", InCorrectValueDisplayText="Enabled" } },
            { RunBasicDialogStates.CheckNetsh, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Exist", InCorrectValueDisplayText="NotExist" } },
            { RunBasicDialogStates.CheckTimestamps, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Exist", InCorrectValueDisplayText="NotExist" } },
            { RunBasicDialogStates.CheckAdGuard, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckKiller, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckIntelConnectivityNetwork, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckCheckPointServices, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckSmartByte, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckVPNs, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckDNS, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "ServiceRunning", InCorrectValueDisplayText="ServiceStopped" } },
            { RunBasicDialogStates.CheckWinDivert, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckAnotherComponents, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
            { RunBasicDialogStates.CheckAnotherComponentsServices, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunning" } },
        };

        public async Task<Dictionary<RunBasicDialogStates, bool>> RunBasicDiagnostic()
        {
            await ComponentTasksManager.Instance.StopAllTasks();
            _ = ProcessService.StopService();
            CurrentStateChanged?.Invoke(RunBasicDialogStates.Preparing);

            await Task.Delay(1000);
            Services = null;
            Dictionary<RunBasicDialogStates, bool> completedStates = [];

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckBFE);
            bool isRunningBFE = IsServiceRunning("BFE");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckProxy);
            bool isProxyEnabled = WindowsNativeProxySettingsService.IsProxyEnabled();

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckNetsh);
            bool isNetshExistInPATH = ExistsOnPath("netsh.exe");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckTimestamps);
            bool isTimestampsChecked = await CheckTimestamps();

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckAdGuard);
            bool isAdGuardRunning = IsProcessRunning("adguardvpnsvc") || IsProcessRunning("adguardvpn") || IsProcessRunning("adguardsvc");
            await Task.Delay(1000);

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckKiller);
            bool isRunningKiller = IsServiceRunning("Killer");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckIntelConnectivityNetwork);
            // The Intel(R) Connectivity Network Service only: "Network" alone matched many
            // Windows services, so the check failed on almost every computer.
            bool isRunningIntelConnectivityNetwork = IsServiceRunning(service =>
                OperatingSystem.IsWindows() && Matches(service, "Intel") && Matches(service, "Connectivity Network"));

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckCheckPointServices);
            bool isCheckPointServiceRunning = IsServiceRunning("TracSrvWrapper") || IsServiceRunning("EPWD");
            await Task.Delay(1000);

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckSmartByte);
            bool isSmartByteRunning = IsServiceRunning("SmartByte");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckVPNs);
            // Running, not merely installed: the dialog reports it as running.
            bool isRunningVPN = IsServiceRunning("vpn");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckDNS);
            bool isUsedDNS = await GetDnsAddress();

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckWinDivert);
            bool isWinDivertRunning = IsServiceRunning("windivert");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckAnotherComponentsServices);
            bool isAnotherServiceComponentRunning = IsServiceExist("GoodbyeDPI") || IsServiceExist("discordfix_zapret") || IsServiceExist("winws1") || IsServiceExist("winws2");
            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckAnotherComponents);
            bool isAnotherComponentRunning = IsProcessRunning("winws") || IsProcessRunning("winws2") || IsProcessRunning("goodbyedpi");
            await Task.Delay(1000);

            completedStates = new()
            {
                { RunBasicDialogStates.CheckBFE, isRunningBFE },
                { RunBasicDialogStates.CheckProxy, isProxyEnabled },
                { RunBasicDialogStates.CheckNetsh, isNetshExistInPATH },
                { RunBasicDialogStates.CheckTimestamps, isTimestampsChecked },
                { RunBasicDialogStates.CheckAdGuard, isAdGuardRunning },
                { RunBasicDialogStates.CheckKiller, isRunningKiller },
                { RunBasicDialogStates.CheckIntelConnectivityNetwork, isRunningIntelConnectivityNetwork },
                { RunBasicDialogStates.CheckCheckPointServices, isCheckPointServiceRunning },
                { RunBasicDialogStates.CheckSmartByte, isSmartByteRunning },
                { RunBasicDialogStates.CheckVPNs, isRunningVPN },
                { RunBasicDialogStates.CheckDNS, isUsedDNS },
                { RunBasicDialogStates.CheckWinDivert, isWinDivertRunning },
                { RunBasicDialogStates.CheckAnotherComponents, isAnotherComponentRunning },
                { RunBasicDialogStates.CheckAnotherComponentsServices, isAnotherServiceComponentRunning },
            };

            await Task.CompletedTask;
            Services = null;

            CurrentStateChanged?.Invoke(RunBasicDialogStates.Completed);

            return completedStates;
        }

        public async Task<Dictionary<RunBasicDialogStates, FixResultModel>> FixAllBasicErrors(Dictionary<RunBasicDialogStates, bool> statesList)
        {
            Dictionary<RunBasicDialogStates, FixResultModel> result = [];
            foreach (var state in statesList)
            {
                if (state.Value != (BasicDiagnosticStateModelPairs.FirstOrDefault(x => x.Key == state.Key).Value?.CorrectState ?? false))
                {
                    result.Add(state.Key, await TryToFixBasicError(state.Key));
                }
            }
            await Task.Delay(1000);
            return result;
        }

        public class FixResultModel
        {
            public bool IsFixed { get; set; }
            public string? ErrorCode { get; set; }
        }

        public async Task<FixResultModel> TryToFixBasicError(RunBasicDialogStates state)
        {
            FixResultModel fixResultModel = new() { IsFixed = false, ErrorCode = "INFO_CANNOT_FIX" };
            switch (state)
            {
                case RunBasicDialogStates.CheckTimestamps:
                    fixResultModel.IsFixed = await EnableTimestamps();
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case RunBasicDialogStates.CheckWinDivert:
                    // The tray stops the service on its own time: success is reported only when
                    // the service is really gone.
                    await ProcessService.StopService();
                    fixResultModel.IsFixed = false;
                    for (int i = 0; i < 20 && !fixResultModel.IsFixed; i++)
                    {
                        await Task.Delay(500);
                        Services = null;
                        fixResultModel.IsFixed = !IsServiceRunning("windivert");
                    }
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                default:
                    fixResultModel.IsFixed = false;
                    fixResultModel.ErrorCode = "INFO_CANNOT_FIX";
                    break;
            }
            await Task.CompletedTask;
            return fixResultModel;
        }

        public async Task<FixResultModel> TryToFixStoreError(StoreCheckStates state)
        {
            FixResultModel fixResultModel = new() { IsFixed = false, ErrorCode = "INFO_CANNOT_FIX" };

            string localAppData = Core.Data.Directories.DataDirectory;
            string storeFolder = Directories.StoreDirectory;

            switch (state)
            {
                case StoreCheckStates.CheckWriteAccess:
                    fixResultModel.IsFixed = await GrantAccess(storeFolder);
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case StoreCheckStates.CheckWriteAccessIntoDatabaseFolder:
                    fixResultModel.IsFixed = await GrantAccess(Directories.StoreLocalCacheDirectory);
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case StoreCheckStates.CheckWriteAccessIntoItemsFolder:
                    fixResultModel.IsFixed = await GrantAccess(Directories.StoreItemsDirectory);
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case StoreCheckStates.CheckWriteAccessIntoRepoFolder:
                    fixResultModel.IsFixed = await GrantAccess(Directories.StoreRepoCacheDirectory);
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                default:
                    fixResultModel.IsFixed = false;
                    fixResultModel.ErrorCode = "INFO_CANNOT_FIX";
                    break;
            }
            await Task.CompletedTask;
            return fixResultModel;
        }

        #region BasicFixServices

        ServiceController[]? Services;

        private bool IsServiceExist(string name)
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return true;
                Services ??= ServiceController.GetServices();
                // A loop, not a lambda: the platform analyzer sees the Windows check above.
                foreach (var service in Services)
                {
                    if (service.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsServiceRunning(string name) =>
            IsServiceRunning(service =>
                OperatingSystem.IsWindows() && service.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase));

        // Any matching service that runs: the first match alone could be a stopped one.
        private bool IsServiceRunning(Func<ServiceController, bool> match)
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return true;
                Services ??= ServiceController.GetServices();
                foreach (var service in Services)
                {
                    if (match(service) &&
                        service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StopPending or ServiceControllerStatus.StartPending)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool Matches(ServiceController service, string text) =>
            service.ServiceName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            service.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);

        private static bool ExistsOnPath(string fileName)
        {
            try
            {
                return GetFullPath(fileName) != null;
            }
            catch
            {
                return false;
            }
        }

        private static string? GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        private static readonly string PowerShellPath =
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

        /// <summary>
        /// Runs a system tool without a console window and without blocking the calling thread
        /// </summary>
        private static async Task<(int ExitCode, string Output)> RunToolAsync(string fileName, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Cannot start {fileName}");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); }
                catch (Exception ex)
                {
                    // For example, the process exited at the same time; the timeout is reported below.
                    Logger.Instance.CreateWarningLog(nameof(TroubleshootingService), $"Cannot stop {fileName}: {ex.Message}");
                }
                throw new System.TimeoutException($"{fileName} did not finish in time");
            }
            await errors;
            return (process.ExitCode, await output);
        }

        private async Task<bool> EnableTimestamps()
        {
            try
            {
                var result = await RunToolAsync(Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                    "interface", "tcp", "set", "global", "timestamps=enabled");
                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(TroubleshootingService), $"Cannot enable timestamps. {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckTimestamps()
        {
            try
            {
                // netsh prints localized text and cannot pipe into findstr without a shell;
                // Get-NetTCPSetting returns the state as Enabled or Disabled in any language.
                var result = await RunToolAsync(PowerShellPath, "-NoProfile", "-NonInteractive", "-Command",
                    "(Get-NetTCPSetting -SettingName Internet).Timestamps");
                return result.ExitCode == 0 &&
                    string.Equals(result.Output.Trim(), "Enabled", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(TroubleshootingService), $"Cannot check timestamps. {ex.Message}");
                return false;
            }
        }

        private static bool IsProcessRunning(string procName)
        {
            Process[] processes =
                Process.GetProcessesByName(procName);
            // Each Process holds a handle until it is disposed.
            foreach (var process in processes)
                process.Dispose();
            return processes.Length > 0;
        }

        private static async Task<bool> GetDnsAddress()
        {
            try
            {
                var result = await RunToolAsync(PowerShellPath, "-NoProfile", "-NonInteractive", "-Command",
                    "Get-ChildItem -Recurse -Path 'HKLM:System\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\' | Get-ItemProperty | Where-Object { $_.DohFlags -gt 0 } | Measure-Object | Select-Object -ExpandProperty Count");

                return int.TryParse(result.Output.Trim(), out int count) && count != 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(TroubleshootingService), $"Cannot check DNS. {ex.Message}");
                return false;
            }
        }

        #endregion

        #region StoreFixServices
        private TaskCompletionSource<bool>? _tcs;
        // The answer of the tray can be lost; without a time limit the fix waited forever.
        private static readonly TimeSpan GrantAccessTimeout = TimeSpan.FromSeconds(30);

        private async Task<bool> GrantAccess(string file, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcs = tcs;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(GrantAccessTimeout);
            using (cts.Token.Register(() => tcs.TrySetResult(false)))
            {
                await PipeHelper.SendGrantAccessPacket(file);
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public void OnGrantAccessCompleted(bool state)
        {
            // An answer without a request is ignored.
            _tcs?.TrySetResult(state);
        }

        private static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
        {
            try
            {
                using FileStream fs = File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose);
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }

        #endregion
    }
}
