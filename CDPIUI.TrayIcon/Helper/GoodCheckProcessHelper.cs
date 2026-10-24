using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

namespace CDPIUI.TrayIcon.Helper
{
    public class GoodCheckProcessHelper
    {
        private static GoodCheckProcessHelper? _instance;
        private static readonly object _lock = new object();

        public static GoodCheckProcessHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new GoodCheckProcessHelper();
                    return _instance;
                }
            }
        }

        // One run at a time: a second start replaced the process, the job and the token source of
        // the running one.
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private CancellationTokenSource? _cancellationTokenSource;

        private Process? Process;

        public GoodCheckProcessHelper() 
        { 
            PipeServer.Instance.Disconnected += () =>
            {
                Stop();
            };
        }

        public async Task<bool> StartAsync(string executable, string args, string operationId)
        {
            if (!await _runLock.WaitAsync(0))
            {
                Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessHelper), "Rejected GoodCheck start: another GoodCheck run is in progress.");
                HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                return false;
            }
            try
            {
                return await RunAsync(executable, args, operationId);
            }
            finally
            {
                _runLock.Release();
            }
        }

        private async Task<bool> RunAsync(string executable, string args, string operationId)
        {
            string? unsafeOption = FileWritingOptions.FindOutsideWrite(
                args, TrustedPaths.AllowedRoots, Path.GetDirectoryName(executable), followArgumentFiles: true);
            if (unsafeOption != null)
            {
                Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessHelper), $"Rejected GoodCheck start: the arguments write a file outside the application directories: {unsafeOption}");
                HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                return false;
            }

            using ExecutableLease? lease = await ExecutableTrust.AcquireAsync(executable, "GoodCheck start");
            if (lease == null)
            {
                HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                return false;
            }

            await TasksHelper.Instance.StopAllTasks();

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            Process? process = null;
            nint job = IntPtr.Zero;
            await PipeHelper.SendGoodCheckPacket(Shared.Pipe.Models.GoodCheckMessageIds.Running, operationId);
            await TasksHelper.Instance.ApplyStatusToAllTasks(false);
            // The component buttons are blocked while GoodCheck runs; they stayed blocked when
            // the job could not be created, and the job handle was never closed.
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                };

                job = CreateKillOnCloseJob();

                process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                Process = process;

                try
                {
                    if (!process.Start())
                    {
                        HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_UNABLE_START_PROCESS details => {ex.Message}");
                    HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                    return false;
                }
                // The lease (using above) stays until the run ends: the process maps its DLLs after
                // Start returns and can load more later, so they must not be replaceable meanwhile.

                if (!AssignProcessToJobObject(job, process.Handle))
                {
                    TryKillProcess(process);
                    HandleProcessException("ERR_WIN32_EXCEPTION", operationId);
                    Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_WIN32_EXCEPTION details => {Marshal.GetLastWin32Error()}");
                    return false;
                }

                var completedTask = await Task.WhenAny(Task.Run(() =>
                {
                    process.WaitForExit();
                }, cancellationTokenSource.Token)).ConfigureAwait(false);

                TryKillProcess(process);
                await PipeHelper.SendGoodCheckPacket(Shared.Pipe.Models.GoodCheckMessageIds.Died, operationId);
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_WIN32_EXCEPTION details => {ex.Message}");
                HandleProcessException("ERR_WIN32_EXCEPTION", operationId);
                return false;
            }
            finally
            {
                if (job != IntPtr.Zero)
                {
                    CloseHandle(job);
                }
                Process = null;
                process?.Dispose();
                _cancellationTokenSource = null;
                cancellationTokenSource.Dispose();
                await TasksHelper.Instance.ApplyStatusToAllTasks(true);
            }
        }

        // The run that owns the process and the token source disposes them when it ends.
        public void Stop()
        {
            var process = Process;
            if (process != null) TryKillProcess(process);
            try
            {
                _cancellationTokenSource?.Cancel();
            } 
            catch (ObjectDisposedException)
            {
                // The run has just ended and disposed its token source.
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessHelper), $"Cannot cancel the GoodCheck run: {ex.Message}");
            }
        }
        

        private void TryKillProcess(Process proc)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(GoodCheckProcessHelper), $"Cannot stop the GoodCheck process: {ex.Message}");
            }
        }

        private void HandleProcessException(string error, string operationId)
        {

            _ = PipeHelper.SendGoodCheckPacket(Shared.Pipe.Models.GoodCheckMessageIds.DiedViaError, operationId, error);
        }

        #region WinAPI

        const int JobObjectExtendedLimitInformation = 9;
        const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        static IntPtr CreateKillOnCloseJob()
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr p = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, p, false);
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, p, (uint)length))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }

            return job;
        }
        #endregion
    }
}
