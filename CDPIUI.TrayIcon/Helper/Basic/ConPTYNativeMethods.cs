using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Win32 declarations for running a process in a pseudo console.
/// </summary>
internal static class ConPTYNativeMethods
{
    public const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
    public const int STARTF_USESTDHANDLES = 0x00000100;
    public const int STARTF_USESHOWWINDOW = 0x00000001;
    public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    public const ushort SW_HIDE = 0;
    public const uint INFINITE = 0xFFFFFFFF;
    public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    public const uint WAIT_OBJECT_0 = 0x000000000;
    public const uint WAIT_TIMEOUT = 0x00000102;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(
        IntPtr hProcess,
        out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW")]
    private static extern bool CreateProcess(
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
    private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll")]
    public static extern int GetSystemDefaultLCID();

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
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
    public struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    public static void CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, bool bInheritHandle)
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

    /// <summary>
    /// Startup information of a hidden process that uses its standard handles and the
    /// pseudo console given in <paramref name="attributeList"/>.
    /// </summary>
    public static STARTUPINFOEX CreateHiddenStartupInfo(IntPtr attributeList)
    {
        return new STARTUPINFOEX
        {
            StartupInfo = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFOEX>(),
                dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
                wShowWindow = SW_HIDE
            },
            lpAttributeList = attributeList
        };
    }

    /// <summary>
    /// Starts <paramref name="exePath"/> attached to the pseudo console of
    /// <paramref name="startupInfo"/>. The caller reads the last Win32 error on failure.
    /// </summary>
    // CreateProcessW: CreateProcessA turned characters outside the ANSI code page of
    // the system into '?' in paths and arguments.
    public static bool StartProcess(
        string exePath,
        string args,
        string workingDirectory,
        ref STARTUPINFOEX startupInfo,
        out PROCESS_INFORMATION processInfo)
    {
        return CreateProcess(
            null,
            new StringBuilder($"\"{exePath}\" {args}"),
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
            IntPtr.Zero,
            workingDirectory,
            ref startupInfo,
            out processInfo);
    }
}
