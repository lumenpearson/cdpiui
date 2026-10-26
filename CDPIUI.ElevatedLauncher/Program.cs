using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace CDPIUI.ElevatedLauncher;

/// <summary>
/// Starts an elevated program of the application with an environment that has no .NET runtime
/// variables. UAC builds the environment of an elevated process from the user profile, so the
/// variables in HKCU\Environment, which every process of the user can change, apply to it.
/// </summary>
internal static class Program
{
    // HRESULT values, as EditHostFile.exe returns them; the GUI converts them to error names.
    private const int InvalidArgument = unchecked((int)0x80070057);
    private const int FileNotFound = unchecked((int)0x80070002);
    private const int Failure = unchecked((int)0x80004005);

    private static int Main(string[] args)
    {
        if (args.Length == 0 || !LaunchPolicy.TryGetTargetFileName(args[0], out string fileName))
            return InvalidArgument;

        string? directory = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(directory))
            return Failure;

        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return FileNotFound;

        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            WorkingDirectory = directory,
        };
        // Each argument is quoted again for the target, so it receives the same arguments.
        for (int i = 1; i < args.Length; i++)
            startInfo.ArgumentList.Add(args[i]);
        LaunchPolicy.RemoveRuntimeVariables(startInfo.Environment);

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process == null)
                return Failure;
            if (!LaunchPolicy.WaitsForExit(args[0]))
                return 0;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            return ex.NativeErrorCode != 0 ? unchecked((int)0x80070000) | (ex.NativeErrorCode & 0xFFFF) : Failure;
        }
    }
}
