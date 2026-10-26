using System;
using System.Runtime.InteropServices;

namespace CDPIUI.Helper.Basic;

internal static class CommandLineSplitter
{
    /// <summary>
    /// Splits a command line the way the runtime splits the arguments of Main. File paths
    /// with spaces and quoted arguments mixed with plain ones were split wrongly by a regex.
    /// </summary>
    internal static string[] Split(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return [];

        IntPtr argv = CommandLineToArgvW(commandLine, out int argc);
        if (argv == IntPtr.Zero)
            return [];
        try
        {
            var result = new string[argc];
            for (int i = 0; i < argc; i++)
                result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? string.Empty;
            return result;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
