using System.IO;

namespace CDPIUI.Shared.Security;

/// <summary>
/// Starts the elevated programs of the application through CDPIUI_ElevatedLauncher.exe. UAC
/// builds the environment of an elevated process from the user profile, so variables that any
/// process of the user can set (HKCU\Environment, for example DOTNET_ROOT or CORECLR_PROFILER_PATH)
/// would make the .NET host of the tray or EditHostFile.exe load code of the user. The launcher
/// is a Native AOT program without a .NET host; it removes these variables and then starts the
/// program. The target names must agree with LaunchPolicy in CDPIUI.ElevatedLauncher.
/// </summary>
public static class ElevatedLauncherCommand
{
    public const string FileName = "CDPIUI_ElevatedLauncher.exe";

    /// <summary>Starts CDPIUI_TrayIcon.exe.</summary>
    public const string TrayTarget = "tray";

    /// <summary>Starts EditHostFile.exe and returns its exit code.</summary>
    public const string HostsTarget = "hosts";

    public static string GetPath(string applicationDirectory)
    {
        return Path.Combine(applicationDirectory, FileName);
    }

    /// <summary>
    /// The command line of the launcher: the target name, then the arguments of the target. The
    /// launcher passes these arguments to the target unchanged.
    /// </summary>
    public static string GetArguments(string target, string targetArguments)
    {
        return string.IsNullOrEmpty(targetArguments) ? target : target + " " + targetArguments;
    }
}
