using System;
using System.Collections.Generic;
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
    private const string CleanupTarget = "cleanup";
    private const string TrayFileName = "CDPIUI_TrayIcon.exe";

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    private static int Main(string[] args)
    {
        if (args.Length == 0)
            return InvalidArgument;

        // The MSI embeds this launcher and invokes this internal target as SYSTEM. Do not run
        // cleanup through the copy in INSTALLLOCATION: that directory is precisely what must be
        // validated before any installed executable is trusted.
        if (string.Equals(args[0], CleanupTarget, StringComparison.Ordinal))
            return RunUninstallCleanup(args);

        if (!LaunchPolicy.TryGetTargetFileName(args[0], out string fileName))
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
            if (!SetSafeSearchPath(startInfo))
                return Failure;

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
        catch
        {
            return Failure;
        }
    }

    private static int RunUninstallCleanup(string[] args)
    {
        if (args.Length != 2)
            return InvalidArgument;

        string directory;
        try
        {
            directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
        }
        catch
        {
            return InvalidArgument;
        }

        try
        {
            string systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value;
            if (!string.Equals(WindowsIdentity.GetCurrent().User?.Value, systemSid, StringComparison.OrdinalIgnoreCase))
                return AccessDenied;

            if (!IsRegisteredMsiInstall(directory) ||
                !ProtectedInstallLocationPolicy.IsProtected(directory, systemSid))
                return AccessDenied;

            string path = Path.Combine(directory, TrayFileName);
            if (!File.Exists(path))
                return FileNotFound;

            var startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                WorkingDirectory = directory,
            };
            startInfo.ArgumentList.Add("--uninstall-cleanup");
            LaunchPolicy.RemoveRuntimeVariables(startInfo.Environment);
            if (!SetSafeSearchPath(startInfo))
                return Failure;

            using Process? process = Process.Start(startInfo);
            if (process == null)
                return Failure;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            return ex.NativeErrorCode != 0 ? unchecked((int)0x80070000) | (ex.NativeErrorCode & 0xFFFF) : Failure;
        }
        catch
        {
            return Failure;
        }
    }

    private static bool SetSafeSearchPath(ProcessStartInfo startInfo)
    {
        string systemDirectory = Environment.SystemDirectory;
        if (string.IsNullOrWhiteSpace(systemDirectory))
            return false;

        string[] candidates =
        [
            systemDirectory,
            Path.Combine(systemDirectory, "Wbem"),
            Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0"),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        ];
        var directories = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in candidates)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && seen.Add(directory))
                directories.Add(directory);
        }

        if (directories.Count == 0)
            return false;

        // PATH comes from the unelevated caller. A user-writable entry could supply a DLL or an
        // unqualified child executable to an elevated target.
        startInfo.Environment["PATH"] = string.Join(Path.PathSeparator.ToString(), directories);
        return true;
    }

    private static bool IsSafeElevationContext(string directory)
    {
        try
        {
            string? trayUserSid = WindowsIdentity.GetCurrent().User?.Value;
            if (IsRegisteredMsiInstall(directory))
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

    private static bool IsRegisteredMsiInstall(string directory)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? installKey = baseKey.OpenSubKey(@"Software\Storik4\CDPI UI");
            if (installKey?.GetValue("InstallLocation") is not string installLocation)
                return false;

            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(installLocation)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),
                StringComparison.OrdinalIgnoreCase);
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
