using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CDPIUI.Shared.PrettyErrorConversionService;
using CDPIUI.Shared.Security;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32.SafeHandles;
using static CDPIUI.TrayIcon.Helper.Basic.ConPTYNativeMethods;

namespace CDPIUI.TrayIcon.Helper;

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

    private readonly string _prefix = string.Empty;

    private StopActionCallers _stopActionCaller = StopActionCallers.Unknown;

    public bool processState { get; private set; } = false;

    // Starts take a ticket before they wait for the lock; a stop records the last ticket given
    // out. A start whose ticket is not newer than the last stop does not run, because the stop
    // came after it, even when the stop got the lock first.
    private long _lastStartTicket;
    private long _lastStopTicket;

    /// <summary>A process runs, or a start is waiting for its turn.</summary>
    public bool IsRunningOrStarting =>
        processState || Interlocked.Read(ref _lastStartTicket) > Interlocked.Read(ref _lastStopTicket);
    private bool _currentState = false;
    private object _setStateLock = new();
    private void ChangeProcessState(bool isRunning)
    {
        lock (_setStateLock)
        {
            if (_currentState != isRunning)
            {
                _currentState = isRunning;
                processState = _currentState;
                ProcessStateChanged?.Invoke(isRunning);
                Debug.WriteLine(processState);
            }
        }
    }

    public ConPTYHelper(string prefix = "")
    {
        _prefix = prefix;
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

    private const uint ProcessExitWaitMs = 5000;

    public static Task<ConPTYCaptureResult> CaptureProcessOutputAsync(
        string exePath,
        string args,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return ConPTYOutputCapture.CaptureAsync(exePath, args, workingDirectory, timeout, cancellationToken);
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

                _stopActionCaller = StopActionCallers.Unknown;
                lock (_outputLock)
                    _outputDefaultBuffer.Clear();

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
        IntPtr hInputRead = IntPtr.Zero;
        IntPtr hOutputWrite = IntPtr.Zero;
        IntPtr lpAttrList = IntPtr.Zero;
        var pi = new PROCESS_INFORMATION();

        var outputReader = new ConPTYOutputReader(_prefix, AppendOutput);
        ExecutableLease? lease = null;

        try
        {
            // The arguments come from the UI: options that write a file must stay in the
            // application directories and Lua code must be approved, because the component runs
            // with administrator rights. The files they refer to stay locked with the program.
            lease = await ExecutableTrust.AcquireAsync(
                exePath,
                args,
                string.IsNullOrWhiteSpace(workingDirectory) ? Path.GetDirectoryName(exePath) : workingDirectory,
                "process start")
                ?? throw new UnauthorizedAccessException("The executable is outside the application directories, its arguments are not allowed, or it was not approved.");

            CreatePseudoConsoleWithPipes(ref hInputRead, ref hOutputWrite);
            lpAttrList = CreateAttributeList();
            StartProcess(exePath, args, workingDirectory, lpAttrList, ref pi);
            // The lease stays until the process exits (finally below): the child maps its DLLs
            // after CreateProcess returns and can load more later, and a user process could
            // replace them in the item folder the moment the lease was released.

            _processInfo = pi;
            ChildProcessJob.Assign(pi.hProcess);

            CloseHandle(pi.hThread);

            var safeOutputReadHandle = new SafeFileHandle(_hOutputRead, ownsHandle: true);
            _hOutputRead = IntPtr.Zero;

            _ = Task.Run(() => ConPTYProcessWatcher.CancelOnExitAsync(pi, cancellationTokenSource));

            await outputReader.ReadAsync(safeOutputReadHandle, token);
        }
        catch (OperationCanceledException)
        {
            PrettyErrorCode lastError = outputReader.LastError;
            Logger.Instance.CreateInfoLog(nameof(ConPTYHelper), $"Process will be stopped {lastError}.");
            if (lastError != PrettyErrorCode.SUCCESS && _stopActionCaller == StopActionCallers.Unknown)
                ShowErrorMessage(lastError);
        }
        catch (Exception ex)
        {
            if (ex.Message != "External component has thrown an exception.")
                ShowErrorMessage(ErrorHelper.Convertor.GetLoggedErrorCode(nameof(ConPTYHelper), ex), _object: "console");

        }
        finally
        {
            // CloseRunHandles ends and waits for a process that still runs (stop and cancel
            // paths); only then may its files change.
            CloseRunHandles(pi, lpAttrList, hInputRead, hOutputWrite);
            lease?.Dispose();

            SendStopMessage("Process will be stopped");

            processState = false;
            ChangeProcessState(processState);
            _processInfo = default;
        }
    }

    // The console sides of the pipes are closed here once the pseudo console holds them; the
    // caller closes them when CreatePseudoConsole fails.
    private void CreatePseudoConsoleWithPipes(ref IntPtr hInputRead, ref IntPtr hOutputWrite)
    {
        // Each handle is stored before the next call, so the cleanup closes it if that call throws.
        CreatePipe(out hInputRead, out IntPtr hInputWrite, false);
        _hInputWrite = hInputWrite;
        CreatePipe(out IntPtr hOutputRead, out hOutputWrite, false);
        _hOutputRead = hOutputRead;

        var size = new COORD { X = 80, Y = 25 };
        var hr = CreatePseudoConsole(size, hInputRead, hOutputWrite, 0, out IntPtr pseudoConsoleHandle);

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
    }

    // Returns the initialized list that holds the pseudo console; frees it itself on failure.
    private IntPtr CreateAttributeList()
    {
        var lpSize = IntPtr.Zero;

        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        IntPtr lpAttrList = Marshal.AllocHGlobal(lpSize);
        if (!InitializeProcThreadAttributeList(lpAttrList, 1, 0, ref lpSize))
        {
            // Read before FreeHGlobal, which can change the last error.
            int error = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(lpAttrList);
            throw new Win32Exception(error, "Cannot initialize process attributes.");
        }

        UpdateProcThreadAttribute(lpAttrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _pseudoConsoleHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);
        return lpAttrList;
    }

    private static void StartProcess(string exePath, string args, string workingDirectory, IntPtr lpAttrList, ref PROCESS_INFORMATION pi)
    {
        var si = CreateHiddenStartupInfo(lpAttrList);
        var success = ConPTYNativeMethods.StartProcess(exePath, args, workingDirectory, ref si, out pi);

        if (!success)
        {
            // Win32Exception keeps the error code, so "file not found" or "access denied"
            // reach the GUI as such instead of an unknown error.
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot start process.");
        }
    }

    private void AppendOutput(string output)
    {
        lock (_outputLock)
        {
            if (_outputDefaultBuffer.Length > 5000)
            {
                _outputDefaultBuffer.Clear();
            }

            _outputDefaultBuffer.Append(output);
        }

        OutputAdded?.Invoke(output);
    }

    private void CloseRunHandles(PROCESS_INFORMATION pi, IntPtr lpAttrList, IntPtr hInputRead, IntPtr hOutputWrite)
    {
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

    public async Task StopProcess(bool output = true)
    {
        _stopActionCaller = StopActionCallers.User;
        InterlockedMax(ref _lastStopTicket, Interlocked.Read(ref _lastStartTicket));
        ConPTYProcessWatcher.TryCancel(_cancellationTokenSource);

        // The worker holds the lock until its process ends. A start that took the lock before
        // this stop is stopped as well, because the stop came after it.
        while (!await _processLock.WaitAsync(100))
            ConPTYProcessWatcher.TryCancel(_cancellationTokenSource);

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
        string prefix = string.IsNullOrEmpty(_prefix) ? "" : $" [{_prefix}]";
        lock (_outputLock)
            _outputDefaultBuffer.Append($"\n[PSEUDOCONSOLE]{prefix} {output}");

        ProcessExited?.Invoke($"\n[PSEUDOCONSOLE]{prefix} {output}");
    }

    private void ShowErrorMessage(PrettyErrorCode errorCode, string _object = "process")
    {
        Logger.Instance.CreateWarningLog(nameof(ProcessManager), $"CONPTY error: {errorCode} object: {_object}");
        ErrorHappens?.Invoke(Tuple.Create(errorCode, _object));
    }

    public string GetDefaultOutput()
    {
        lock (_outputLock)
            return _outputDefaultBuffer.ToString();
    }
}
