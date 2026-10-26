using System.Runtime.InteropServices;
using System.Text;
using CDPIUI.Shared.Security;
using Microsoft.Win32.SafeHandles;
using static CDPIUI.TrayIcon.Helper.Basic.ConPTYNativeMethods;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Runs an executable once in a pseudo console and collects all of its output, for example
/// the help text of a component. One instance serves one capture and owns its handles.
/// </summary>
internal sealed class ConPTYOutputCapture
{
    private readonly StringBuilder _output = new();
    private IntPtr _pseudoConsoleHandle = IntPtr.Zero;
    private IntPtr _hInputRead = IntPtr.Zero;
    private IntPtr _hInputWrite = IntPtr.Zero;
    private IntPtr _hOutputRead = IntPtr.Zero;
    private IntPtr _hOutputWrite = IntPtr.Zero;
    private IntPtr _attributeList = IntPtr.Zero;
    private PROCESS_INFORMATION _processInfo;
    private Task? _outputTask;

    private ConPTYOutputCapture()
    {
    }

    public static Task<ConPTYCaptureResult> CaptureAsync(
        string exePath,
        string args,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return new ConPTYOutputCapture().RunAsync(exePath, args, workingDirectory, timeout, cancellationToken);
    }

    private async Task<ConPTYCaptureResult> RunAsync(
        string exePath,
        string args,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ExecutableLease? lease = null;

        try
        {
            lease = await ExecutableTrust.AcquireAsync(exePath, "help output capture")
                ?? throw new UnauthorizedAccessException("The executable is outside the application directories or was not approved.");

            CreatePseudoConsoleWithPipes();
            CreateAttributeList();
            StartProcess(exePath, args, workingDirectory);
            // Kept until the process exits (finally below), like the lease of a component start.

            ChildProcessJob.Assign(_processInfo.hProcess);
            CloseHandle(_processInfo.hThread);
            _processInfo.hThread = IntPtr.Zero;

            SafeFileHandle safeOutputHandle = new(_hOutputRead, ownsHandle: true);
            _hOutputRead = IntPtr.Zero;
            _outputTask = ReadOutputAsync(safeOutputHandle, _output);

            return await WaitForResultAsync(timeout, cancellationToken);
        }
        finally
        {
            lease?.Dispose();
            CloseHandles();

            if (_outputTask != null && !_outputTask.IsCompleted)
            {
                try
                {
                    await _outputTask;
                }
                catch
                {
                    // The capture has already failed and its handles are closed; a read error
                    // here only follows from that and must not replace the first exception.
                }
            }
        }
    }

    private void CreatePseudoConsoleWithPipes()
    {
        CreatePipe(out _hInputRead, out _hInputWrite, false);
        CreatePipe(out _hOutputRead, out _hOutputWrite, false);

        COORD size = new() { X = 4096, Y = 50 };
        uint result = CreatePseudoConsole(
            size,
            _hInputRead,
            _hOutputWrite,
            0,
            out _pseudoConsoleHandle);
        if (result != 0)
        {
            throw new InvalidOperationException(
                $"Unable to create PseudoConsole, error: {result}");
        }

        CloseHandle(_hInputRead);
        _hInputRead = IntPtr.Zero;
        CloseHandle(_hOutputWrite);
        _hOutputWrite = IntPtr.Zero;
    }

    private void CreateAttributeList()
    {
        IntPtr attributeListSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
        _attributeList = Marshal.AllocHGlobal(attributeListSize);
        if (!InitializeProcThreadAttributeList(_attributeList, 1, 0, ref attributeListSize))
        {
            throw new InvalidOperationException(
                $"Cannot initialize process attributes, error: {Marshal.GetLastWin32Error()}");
        }

        if (!UpdateProcThreadAttribute(
            _attributeList,
            0,
            (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            _pseudoConsoleHandle,
            (IntPtr)IntPtr.Size,
            IntPtr.Zero,
            IntPtr.Zero))
        {
            throw new InvalidOperationException(
                $"Cannot attach PseudoConsole, error: {Marshal.GetLastWin32Error()}");
        }
    }

    private void StartProcess(string exePath, string args, string workingDirectory)
    {
        STARTUPINFOEX startupInfo = CreateHiddenStartupInfo(_attributeList);
        string resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
            : workingDirectory;
        bool started = ConPTYNativeMethods.StartProcess(
            exePath,
            args,
            resolvedWorkingDirectory,
            ref startupInfo,
            out _processInfo);
        if (!started)
        {
            throw new InvalidOperationException(
                $"Cannot start process, error: {Marshal.GetLastWin32Error()}");
        }
    }

    private async Task<ConPTYCaptureResult> WaitForResultAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        bool timedOut = false;
        bool canceled = false;
        int exitCode = -1;

        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await WaitForProcessExitAsync(_processInfo, linkedSource.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = cancellationToken.IsCancellationRequested;
            timedOut = !canceled && timeoutSource.IsCancellationRequested;
            TerminateProcess(_processInfo.hProcess, 1);
            WaitForSingleObject(_processInfo.hProcess, INFINITE);
        }

        if (GetExitCodeProcess(_processInfo.hProcess, out uint nativeExitCode))
        {
            exitCode = unchecked((int)nativeExitCode);
        }

        ClosePseudoConsole(_pseudoConsoleHandle);
        _pseudoConsoleHandle = IntPtr.Zero;
        CloseHandle(_hInputWrite);
        _hInputWrite = IntPtr.Zero;

        try
        {
            await _outputTask!;
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
            Output = _output.ToString(),
            ExitCode = exitCode,
            TimedOut = timedOut,
        };
    }

    private void CloseHandles()
    {
        if (_processInfo.hProcess != IntPtr.Zero)
        {
            if (WaitForSingleObject(_processInfo.hProcess, 0) == WAIT_TIMEOUT)
            {
                TerminateProcess(_processInfo.hProcess, 1);
                WaitForSingleObject(_processInfo.hProcess, INFINITE);
            }
            CloseHandle(_processInfo.hProcess);
        }
        if (_processInfo.hThread != IntPtr.Zero)
        {
            CloseHandle(_processInfo.hThread);
        }
        if (_attributeList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
        }
        if (_pseudoConsoleHandle != IntPtr.Zero)
        {
            ClosePseudoConsole(_pseudoConsoleHandle);
        }
        if (_hInputRead != IntPtr.Zero)
        {
            CloseHandle(_hInputRead);
        }
        if (_hInputWrite != IntPtr.Zero)
        {
            CloseHandle(_hInputWrite);
        }
        if (_hOutputRead != IntPtr.Zero)
        {
            CloseHandle(_hOutputRead);
        }
        if (_hOutputWrite != IntPtr.Zero)
        {
            CloseHandle(_hOutputWrite);
        }
    }

    private static async Task ReadOutputAsync(
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
}
