using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using CDPIUI.Shared.Security;

namespace CDPIUI.ElevatedLauncher;

/// <summary>
/// Starts an elevated program of the application with an environment that has no .NET runtime
/// variables. UAC builds the environment of an elevated process from the user profile, so the
/// variables in HKCU\Environment, which every process of the user can change, apply to it.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class Program
{
    // HRESULT values, as EditHostFile.exe returns them; the GUI converts them to error names.
    private const int InvalidArgument = unchecked((int)0x80070057);
    private const int FileNotFound = unchecked((int)0x80070002);
    private const int AccessDenied = unchecked((int)0x80070005);
    private const int Failure = unchecked((int)0x80004005);

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || !LaunchPolicy.TryGetTargetFileName(args[0], out string fileName))
            return InvalidArgument;

        string? directory = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(directory))
            return Failure;

        if (!IsSafeElevationContext(directory))
            return AccessDenied;

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

    private static bool IsSafeElevationContext(string directory)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? installKey = baseKey.OpenSubKey(@"Software\Storik4\CDPI UI");
            string? trayUserSid = WindowsIdentity.GetCurrent().User?.Value;
            if (installKey?.GetValue("InstallLocation") is string installLocation &&
                string.Equals(
                    Path.GetFullPath(installLocation).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return ProtectedInstallLocationPolicy.IsProtected(directory, trayUserSid);
            }

            string? desktopUserSid = GetDesktopUserSid()?.Value;
            string? directoryOwnerSid = new DirectoryInfo(directory)
                .GetAccessControl(AccessControlSections.Owner)
                .GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner
                    ? owner.Value
                    : null;

            return PortableElevationPolicy.IsAllowed(
                false, trayUserSid, desktopUserSid, directoryOwnerSid);
        }
        catch
        {
            return false;
        }
    }

    private static SecurityIdentifier? GetDesktopUserSid()
    {
        IntPtr shellWindow = GetShellWindow();
        if (shellWindow == IntPtr.Zero || GetWindowThreadProcessId(shellWindow, out uint processId) == 0)
            return null;

        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
            return null;

        IntPtr token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(process, TokenQuery, out token))
                return null;

            using var identity = new WindowsIdentity(token);
            return identity.User;
        }
        finally
        {
            if (token != IntPtr.Zero)
                CloseHandle(token);
            CloseHandle(process);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
