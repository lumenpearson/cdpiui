using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using CDPIUI.Shared.Extensions;

namespace CDPIUI.TrayIcon.Helper
{
    public sealed class ConPTYCaptureResult
    {
        public string Output { get; init; } = string.Empty;
        public int ExitCode { get; init; }
        public bool TimedOut { get; init; }
    }

    enum StopActionCallers
    {
        Unknown,
        User
    }
    public class ConPTYHelper
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private PROCESS_INFORMATION _processInfo;
        private IntPtr _pseudoConsoleHandle = IntPtr.Zero;
        private IntPtr _hInputWrite = IntPtr.Zero;
        private IntPtr _hOutputRead = IntPtr.Zero;

        private readonly StringBuilder _outputDefaultBuffer;
        // The reader thread writes the buffer while pipe commands read it.
        private readonly object _outputLock = new();

        public Action<bool>? ProcessStateChanged;
        public Action<Tuple<PrettyErrorCode, string>>? ErrorHappens;
        public Action<string>? ProcessExited;
        public Action<string>? OutputAdded;

        private readonly string Prefix = string.Empty;

        private StopActionCallers StopActionCaller = StopActionCallers.Unknown;

        // Checked in this order and the first match of a line wins: with the dictionary every
        // matching key overwrote the previous one, so "Error opening filter" became UNKNOWN
        // through the generic "error" key.
        static readonly (string Text, PrettyErrorCode Code)[] errorMappings =
        {
            ("Error opening filter", PrettyErrorCode.FILTER_OPEN_ERROR),
            ("unknown option", PrettyErrorCode.PARAMETER_ERROR),
            ("hostlists load failed", PrettyErrorCode.HOSTLIST_LOAD_ERROR),
            ("must specify port filter", PrettyErrorCode.PORT_FILTER_ERROR),
            ("must specify port or/and partial raw filter", PrettyErrorCode.PORT_FILTER_WRONG_VALUE_ERROR),
            ("ERROR:", PrettyErrorCode.UNKNOWN),
            ("Component not installed correctly", PrettyErrorCode.COMPONENT_INSTALL_ERROR),
            ("error", PrettyErrorCode.UNKNOWN),
            ("invalid value", PrettyErrorCode.INVALID_VALUE_ERROR),
            ("nvalid value", PrettyErrorCode.INVALID_VALUE_ERROR),
            ("option requires an argument", PrettyErrorCode.INVALID_VALUE_ERROR),
            ("--debug=0|1|syslog|@<filename>", PrettyErrorCode.PARAMETER_ERROR),
            ("already running", PrettyErrorCode.ALREADY_RUNNING_WARN),
            ("could not read", PrettyErrorCode.FILE_READ_ERROR),
            ("flag provided but not defined:", PrettyErrorCode.PARAMETER_ERROR),
            ("cannot create", PrettyErrorCode.ACCESS_DENIED),
            ("cannot access", PrettyErrorCode.ACCESS_DENIED)
        };

        private static PrettyErrorCode? FindError(string output)
        {
            PrettyErrorCode? generic = null;
            foreach (var (text, code) in errorMappings)
            {
                if (!output.Contains(text, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (code != PrettyErrorCode.UNKNOWN)
                    return code;
                generic ??= code;
            }
            return generic;
        }

        public bool processState { get; private set; } = false;

        // Starts take a ticket before they wait for the lock; a stop records the last ticket given
        // out. A start whose ticket is not newer than the last stop does not run, because the stop
        // came after it, even when the stop got the lock first.
        private long _lastStartTicket;
        private long _lastStopTicket;

        /// <summary>A process runs, or a start is waiting for its turn.</summary>
        public bool IsRunningOrStarting =>
            processState || Interlocked.Read(ref _lastStartTicket) > Interlocked.Read(ref _lastStopTicket);
        private bool CurrentState = false;
        private object _setStateLock = new();
        private void ChangeProcessState(bool isRunning)
        {
            lock (_setStateLock)
            {
                if (CurrentState != isRunning)
                {
                    CurrentState = isRunning;
                    processState = CurrentState;
                    ProcessStateChanged?.Invoke(isRunning);
                    Debug.WriteLine(processState);
                }
            }
        }

        public ConPTYHelper(string prefix = "") 
        {
            Prefix = prefix;
            _outputDefaultBuffer = new StringBuilder();
        }

        public bool IsReadyToRunNewProcess()
        {
            if (_processInfo.hProcess != IntPtr.Zero)
            {
                return true;
            }
            return false;
        }

        private readonly SemaphoreSlim _processLock = new SemaphoreSlim(1, 1);

        public static async Task<ConPTYCaptureResult> CaptureProcessOutputAsync(
            string exePath,
            string args,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            IntPtr pseudoConsoleHandle = IntPtr.Zero;
            IntPtr hInputRead = IntPtr.Zero;
            IntPtr hInputWrite = IntPtr.Zero;
            IntPtr hOutputRead = IntPtr.Zero;
            IntPtr hOutputWrite = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero;
            PROCESS_INFORMATION processInfo = default;
            Task? outputTask = null;
            StringBuilder output = new();
            bool timedOut = false;
            bool canceled = false;
            int exitCode = -1;
            ExecutableLease? lease = null;

            try
            {
                lease = await ExecutableTrust.AcquireAsync(exePath, "help output capture")
                    ?? throw new UnauthorizedAccessException("The executable is outside the application directories or was not approved.");

                CreatePipe(out hInputRead, out hInputWrite, false);
                CreatePipe(out hOutputRead, out hOutputWrite, false);

                COORD size = new() { X = 4096, Y = 50 };
                uint result = CreatePseudoConsole(
                    size,
                    hInputRead,
                    hOutputWrite,
                    0,
                    out pseudoConsoleHandle);
                if (result != 0)
                {
                    throw new InvalidOperationException(
                        $"Unable to create PseudoConsole, error: {result}");
                }

                CloseHandle(hInputRead);
                hInputRead = IntPtr.Zero;
                CloseHandle(hOutputWrite);
                hOutputWrite = IntPtr.Zero;

                STARTUPINFOEX startupInfo = new()
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFOEX>(),
                        dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
                        wShowWindow = SW_HIDE,
                    },
                };

                IntPtr attributeListSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
                attributeList = Marshal.AllocHGlobal(attributeListSize);
                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
                {
                    throw new InvalidOperationException(
                        $"Cannot initialize process attributes, error: {Marshal.GetLastWin32Error()}");
                }

                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pseudoConsoleHandle,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new InvalidOperationException(
                        $"Cannot attach PseudoConsole, error: {Marshal.GetLastWin32Error()}");
                }

                startupInfo.lpAttributeList = attributeList;
                string resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
                    : workingDirectory;
                bool started = CreateProcess(
                    null,
                    new StringBuilder($"\"{exePath}\" {args}"),
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                    IntPtr.Zero,
                    resolvedWorkingDirectory,
                    ref startupInfo,
                    out processInfo);
                if (!started)
                {
                    throw new InvalidOperationException(
                        $"Cannot start process, error: {Marshal.GetLastWin32Error()}");
                }
                // Kept until the process exits (finally below), like the lease of a component start.

                ChildProcessJob.Assign(processInfo.hProcess);
                CloseHandle(processInfo.hThread);
                processInfo.hThread = IntPtr.Zero;

                SafeFileHandle safeOutputHandle = new(hOutputRead, ownsHandle: true);
                hOutputRead = IntPtr.Zero;
                outputTask = ReadCaptureOutputAsync(safeOutputHandle, output);

                using CancellationTokenSource timeoutSource = new(timeout);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);
                try
                {
                    await WaitForProcessExitAsync(processInfo, linkedSource.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = cancellationToken.IsCancellationRequested;
                    timedOut = !canceled && timeoutSource.IsCancellationRequested;
                    TerminateProcess(processInfo.hProcess, 1);
                    WaitForSingleObject(processInfo.hProcess, INFINITE);
                }

                if (GetExitCodeProcess(processInfo.hProcess, out uint nativeExitCode))
                {
                    exitCode = unchecked((int)nativeExitCode);
                }

                ClosePseudoConsole(pseudoConsoleHandle);
                pseudoConsoleHandle = IntPtr.Zero;
                CloseHandle(hInputWrite);
                hInputWrite = IntPtr.Zero;

                try
                {
                    await outputTask;
                }
                catch (IOException)
                {
                    // pass
                }

                if (canceled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return new ConPTYCaptureResult
                {
                    Output = output.ToString(),
                    ExitCode = exitCode,
                    TimedOut = timedOut,
                };
            }
            finally
            {
                lease?.Dispose();
                if (processInfo.hProcess != IntPtr.Zero)
                {
                    if (WaitForSingleObject(processInfo.hProcess, 0) == WAIT_TIMEOUT)
                    {
                        TerminateProcess(processInfo.hProcess, 1);
                        WaitForSingleObject(processInfo.hProcess, INFINITE);
                    }
                    CloseHandle(processInfo.hProcess);
                }
                if (processInfo.hThread != IntPtr.Zero)
                {
                    CloseHandle(processInfo.hThread);
                }
                if (attributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }
                if (pseudoConsoleHandle != IntPtr.Zero)
                {
                    ClosePseudoConsole(pseudoConsoleHandle);
                }
                if (hInputRead != IntPtr.Zero)
                {
                    CloseHandle(hInputRead);
                }
                if (hInputWrite != IntPtr.Zero)
                {
                    CloseHandle(hInputWrite);
                }
                if (hOutputRead != IntPtr.Zero)
                {
                    CloseHandle(hOutputRead);
                }
                if (hOutputWrite != IntPtr.Zero)
                {
                    CloseHandle(hOutputWrite);
                }

                if (outputTask != null && !outputTask.IsCompleted)
                {
                    try
                    {
                        await outputTask;
                    }
                    catch
                    {
                        // The capture has already failed and its handles are closed; a read error
                        // here only follows from that and must not replace the first exception.
                    }
                }
            }
        }

        private static async Task ReadCaptureOutputAsync(
            SafeFileHandle outputHandle,
            StringBuilder output)
        {
            using FileStream stream = new(outputHandle, FileAccess.Read, 4096, isAsync: false);
            using StreamReader reader = new(stream, Encoding.UTF8, true, 4096, leaveOpen: false);
            char[] buffer = new char[4096];
            int charsRead;
            while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                output.Append(buffer, 0, charsRead);
            }
        }

        private static async Task WaitForProcessExitAsync(
            PROCESS_INFORMATION processInfo,
            CancellationToken cancellationToken)
        {
            while (WaitForSingleObject(processInfo.hProcess, 100) == WAIT_TIMEOUT)
            {
                await Task.Delay(25, cancellationToken);
            }
        }

        // Not awaited by the callers: the lock is held by a running component until it exits.
        // Catches and logs its own errors.
        public async Task RunProcess(string exePath, string args, string workingDirectory)
        {
            try
            {
                long ticket = Interlocked.Increment(ref _lastStartTicket);
                await _processLock.WaitAsync();
                bool workerStarted = false;
                try
                {
                    if (ticket <= Interlocked.Read(ref _lastStopTicket))
                        return;

                    StopActionCaller = StopActionCallers.Unknown;
                    lock (_outputLock) _outputDefaultBuffer.Clear();

                    var cancellationTokenSource = new CancellationTokenSource();
                    var previousSource = _cancellationTokenSource;
                    _cancellationTokenSource = cancellationTokenSource;
                    previousSource?.Dispose();

                    processState = true;
                    ChangeProcessState(true);

                    // The worker takes over the lock and keeps it until the process ends, so no
                    // stop or other start can come between this start and the worker.
                    _ = Task.Run(() => RunProcessWithConPTY(exePath, args, workingDirectory ?? "", cancellationTokenSource));
                    workerStarted = true;
                }
                catch (Exception ex)
                {
                    ShowErrorMessage(ErrorHelper.Convertor.GetLoggedErrorCode(nameof(ConPTYHelper), ex), _object: "console");
                    SendStopMessage("Unexpected error happens while trying to stop process");

                    processState = false;
                    ChangeProcessState(false);
                }
                finally
                {
                    if (!workerStarted)
                        _processLock.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConPTYHelper), $"Cannot start {exePath}: {ex}");
            }
        }

        // Started by RunProcess, which holds _processLock for it; the lock is released here.
        // Catches and logs its own errors, so a caller can start it without waiting for it.
        private async Task RunProcessWithConPTY(string exePath, string args, string workingDirectory, CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                try
                {
                    await RunProcessWithConPTYCoreAsync(exePath, args, workingDirectory, cancellationTokenSource);
                }
                finally
                {
                    // Released also when an error handler or a state event throws.
                    _processLock.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConPTYHelper), $"Process run of {exePath} failed: {ex}");
            }
        }

        // The caller holds _processLock.
        private async Task RunProcessWithConPTYCoreAsync(string exePath, string args, string workingDirectory, CancellationTokenSource cancellationTokenSource)
        {
            CancellationToken token = cancellationTokenSource.Token;
            IntPtr pseudoConsoleHandle = IntPtr.Zero;
            IntPtr hInputRead = IntPtr.Zero;
            IntPtr hInputWrite = IntPtr.Zero;
            IntPtr hOutputRead = IntPtr.Zero;
            IntPtr hOutputWrite = IntPtr.Zero;
            IntPtr lpAttrList = IntPtr.Zero;
            var pi = new PROCESS_INFORMATION();

            PrettyErrorCode lastError = PrettyErrorCode.SUCCESS;
            ExecutableLease? lease = null;

            try
            {
                // The arguments come from the UI; an option that writes a file must stay in the
                // application directories, because the component runs with administrator rights.
                string? unsafeOption = FileWritingOptions.FindOutsideWrite(
                    args,
                    TrustedPaths.AllowedRoots,
                    string.IsNullOrWhiteSpace(workingDirectory) ? Path.GetDirectoryName(exePath) : workingDirectory,
                    followArgumentFiles: true);
                if (unsafeOption != null)
                    throw new UnauthorizedAccessException($"The arguments write a file outside the application directories: {unsafeOption}");

                lease = await ExecutableTrust.AcquireAsync(exePath, "process start")
                    ?? throw new UnauthorizedAccessException("The executable is outside the application directories or was not approved.");

                CreatePipe(out hInputRead, out hInputWrite, false);
                CreatePipe(out hOutputRead, out hOutputWrite, false);

                _hInputWrite = hInputWrite;
                _hOutputRead = hOutputRead;

                uint consoleSizeX = 80;
                uint consoleSizeY = 25;
                var size = new COORD { X = (short)consoleSizeX, Y = (short)consoleSizeY };
                var hr = CreatePseudoConsole(size, hInputRead, hOutputWrite, 0, out pseudoConsoleHandle);

                if (hr != 0)
                {
                    // An HRESULT: the error converter maps it like other system errors.
                    throw new COMException("Unable to create PseudoConsole.", unchecked((int)hr));
                }

                _pseudoConsoleHandle = pseudoConsoleHandle;

                CloseHandle(hInputRead);
                hInputRead = IntPtr.Zero;
                CloseHandle(hOutputWrite);
                hOutputWrite = IntPtr.Zero;

                var si = new STARTUPINFOEX
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf(typeof(STARTUPINFOEX)),
                        dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
                        wShowWindow = SW_HIDE
                    }
                };

                var lpSize = IntPtr.Zero;

                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                lpAttrList = Marshal.AllocHGlobal(lpSize);
                if (!InitializeProcThreadAttributeList(lpAttrList, 1, 0, ref lpSize))
                {
                    // Read before FreeHGlobal, which can change the last error.
                    int error = Marshal.GetLastWin32Error();
                    Marshal.FreeHGlobal(lpAttrList);
                    lpAttrList = IntPtr.Zero;
                    throw new Win32Exception(error, "Cannot initialize process attributes.");
                }

                UpdateProcThreadAttribute(lpAttrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _pseudoConsoleHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

                si.lpAttributeList = lpAttrList;

                // CreateProcessW: CreateProcessA turned characters outside the ANSI code page of
                // the system into '?' in paths and arguments.
                var success = CreateProcess(
                    null,
                    new StringBuilder($"\"{exePath}\" {args}"),
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref si,
                    out pi);

                if (!success)
                {
                    // Win32Exception keeps the error code, so "file not found" or "access denied"
                    // reach the GUI as such instead of an unknown error.
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot start process.");
                }
                // The lease stays until the process exits (finally below): the child maps its DLLs
                // after CreateProcess returns and can load more later, and a user process could
                // replace them in the item folder the moment the lease was released.

                _processInfo = pi;
                ChildProcessJob.Assign(pi.hProcess);

                CloseHandle(pi.hThread);

                var safeOutputReadHandle = new SafeFileHandle(_hOutputRead, ownsHandle: true);
                _hOutputRead = IntPtr.Zero;

                _ = Task.Run(() => CheckProcessState(pi, cancellationTokenSource));

                using (var reader = new FileStream(safeOutputReadHandle, FileAccess.Read))
                {
                    byte[] buffer = new byte[4096];
                    char[] chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
                    // Keeps a multi-byte character that is split between two reads.
                    Decoder decoder = Encoding.UTF8.GetDecoder();
                    int bytesRead;


                    while (!token.IsCancellationRequested)
                    {
                        bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: token);
                        // The pipe was closed: reading again returns 0 at once and spun the loop.
                        if (bytesRead == 0)
                            break;

                        string _output = new string(chars, 0, decoder.GetChars(buffer, 0, bytesRead, chars, 0));

                        // A specific error stays; a later generic match does not overwrite it.
                        PrettyErrorCode? found = FindError(_output);
                        if (found != null && (lastError == PrettyErrorCode.SUCCESS || found != PrettyErrorCode.UNKNOWN))
                            lastError = found.Value;

                        if (!string.IsNullOrEmpty(Prefix) && !string.IsNullOrEmpty(_output))
                        {
                            string newOutput = string.Empty;
                            foreach (string str in _output.Split("\n"))
                            {
                                newOutput += $"[{Prefix}] {str}\n";
                            }
                            _output = newOutput;
                        }

                        lock (_outputLock)
                        {
                            if (_outputDefaultBuffer.Length > 5000)
                            {
                                _outputDefaultBuffer.Clear();
                            }

                            _outputDefaultBuffer.Append(_output);
                        }

                        OutputAdded?.Invoke(_output);
                    }
                }

            }
            catch (OperationCanceledException)
            {
                
                Logger.Instance.CreateInfoLog(nameof(ConPTYHelper), $"Process will be stopped {lastError}.");
                if (lastError != PrettyErrorCode.SUCCESS && StopActionCaller == StopActionCallers.Unknown) ShowErrorMessage(lastError);
            }
            catch (Exception ex)
            {
                if (ex.Message != "External component has thrown an exception.")
                    ShowErrorMessage(ErrorHelper.Convertor.GetLoggedErrorCode(nameof(ConPTYHelper), ex), _object: "console");

            }
            finally
            {
                lease?.Dispose();
                // The process handle was left open on the cancel and error paths.
                if (pi.hProcess != IntPtr.Zero)
                {
                    if (WaitForSingleObject(pi.hProcess, 0) == WAIT_TIMEOUT)
                    {
                        TerminateProcess(pi.hProcess, 0);
                        WaitForSingleObject(pi.hProcess, ProcessExitWaitMs);
                    }
                    CloseHandle(pi.hProcess);
                }
                if (lpAttrList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(lpAttrList);
                    Marshal.FreeHGlobal(lpAttrList);
                }
                if (_pseudoConsoleHandle != IntPtr.Zero)
                {
                    ClosePseudoConsole(_pseudoConsoleHandle);
                    _pseudoConsoleHandle = IntPtr.Zero;
                }
                if (_hInputWrite != IntPtr.Zero)
                {
                    CloseHandle(_hInputWrite);
                    _hInputWrite = IntPtr.Zero;
                }
                // Pipes of a failed CreatePseudoConsole.
                if (hInputRead != IntPtr.Zero)
                    CloseHandle(hInputRead);
                if (hOutputWrite != IntPtr.Zero)
                    CloseHandle(hOutputWrite);
                if (_hOutputRead != IntPtr.Zero)
                {
                    CloseHandle(_hOutputRead);
                    _hOutputRead = IntPtr.Zero;
                }

                SendStopMessage("Process will be stopped");


                processState = false;
                ChangeProcessState(processState);
                _processInfo = default;
            }
        }

        // Cancels the source of its own run: the field may already hold the source of a newer run.
        private async Task CheckProcessState(PROCESS_INFORMATION pi, CancellationTokenSource cancellationTokenSource)
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (!ProcessAliveCheck(pi))
                {
                    TryCancel(cancellationTokenSource);
                    break;
                }
            }
            await Task.CompletedTask;
        }

        private static void InterlockedMax(ref long target, long value)
        {
            long current = Interlocked.Read(ref target);
            while (value > current)
            {
                long previous = Interlocked.CompareExchange(ref target, value, current);
                if (previous == current)
                    return;
                current = previous;
            }
        }

        private static void TryCancel(CancellationTokenSource? cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A newer run replaced and disposed it; that run ended this process already.
            }
        }

        private static bool ProcessAliveCheck(PROCESS_INFORMATION pi)
        {
            switch (WaitForSingleObject(pi.hProcess, 2000))
            {
                case WAIT_OBJECT_0:
                    return false;

                case WAIT_TIMEOUT:
                    return true;
                default:
                    // WAIT_FAILED: the handle is closed. Reporting it as alive spun the check
                    // loop without any wait.
                    return false;
            }
        }

        public async Task StopProcess(bool output = true)
        {
            StopActionCaller = StopActionCallers.User;
            InterlockedMax(ref _lastStopTicket, Interlocked.Read(ref _lastStartTicket));
            TryCancel(_cancellationTokenSource);

            // The worker holds the lock until its process ends. A start that took the lock before
            // this stop is stopped as well, because the stop came after it.
            while (!await _processLock.WaitAsync(100))
                TryCancel(_cancellationTokenSource);

            try
            {
                // The handle belongs to RunProcessWithConPTY, which closes it; here the process is
                // only ended, and without an endless wait that could block the calling UI thread.
                if (_processInfo.hProcess != IntPtr.Zero)
                {
                    TerminateProcess(_processInfo.hProcess, 0);
                    WaitForSingleObject(_processInfo.hProcess, ProcessExitWaitMs);
                    _processInfo = default;
                }

                if (_pseudoConsoleHandle != IntPtr.Zero)
                {
                    ClosePseudoConsole(_pseudoConsoleHandle);
                    _pseudoConsoleHandle = IntPtr.Zero;
                }
                if (_hInputWrite != IntPtr.Zero)
                {
                    CloseHandle(_hInputWrite);
                    _hInputWrite = IntPtr.Zero;
                }
                processState = false;
                ChangeProcessState(processState);
            }
            catch (Exception)
            {
                processState = false;
                ChangeProcessState(processState);
            }
            finally
            {
                _processLock.Release();
            }
        }

        private void SendStopMessage(string output = "Process will be stopped by user")
        {
            string prefix = string.IsNullOrEmpty(Prefix) ? "" : $" [{Prefix}]";
            lock (_outputLock) _outputDefaultBuffer.Append($"\n[PSEUDOCONSOLE]{prefix} {output}");

            ProcessExited?.Invoke($"\n[PSEUDOCONSOLE]{prefix} {output}");
        }

        private void ShowErrorMessage(PrettyErrorCode errorCode, string _object = "process")
        {
            Logger.Instance.CreateWarningLog(nameof(ProcessManager), $"CONPTY error: {errorCode} object: {_object}");
            ErrorHappens?.Invoke(Tuple.Create(errorCode, _object));
        }

        public string GetDefaultOutput()
        {
            lock (_outputLock) return _outputDefaultBuffer.ToString();
        }

        #region WinAPI Definitions


        private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private const int STARTF_USESTDHANDLES = 0x00000100;
        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const ushort SW_HIDE = 0;
        private const uint INFINITE = 0xFFFFFFFF;
        private const uint ProcessExitWaitMs = 5000;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        private const uint WAIT_OBJECT_0 = 0x000000000;
        private const uint WAIT_TIMEOUT = 0x00000102;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeProcess(
            IntPtr hProcess,
            out System.UInt32 lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW")]
        static extern bool CreateProcess(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [StructLayout(LayoutKind.Sequential)]
        struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        private static void CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, bool bInheritHandle)
        {
            SECURITY_ATTRIBUTES saAttr = new SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
                bInheritHandle = bInheritHandle,
                lpSecurityDescriptor = IntPtr.Zero
            };

            if (!CreatePipe(out hReadPipe, out hWritePipe, ref saAttr, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create PseudoTerminal.");
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();

        #endregion
    }
}
