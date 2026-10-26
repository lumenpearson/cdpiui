using System;
using System.Collections.Generic;

namespace CDPIUI.ElevatedLauncher;

/// <summary>
/// The rules of the launcher: which programs it starts and which environment variables it
/// removes. CDPIUI.Tests compiles this file as well (linked), so the rules are tested without a
/// reference to the Native AOT executable.
/// </summary>
internal static class LaunchPolicy
{
    public const string TrayTarget = "tray";
    public const string HostsTarget = "hosts";

    // Only fixed file names next to the launcher: a path from the command line would let a user
    // process start any program elevated through the UAC prompt of the launcher.
    private static readonly KeyValuePair<string, string>[] Targets =
    [
        new(TrayTarget, "CDPIUI_TrayIcon.exe"),
        new(HostsTarget, "EditHostFile.exe"),
    ];

    // The .NET host and CoreCLR read these variables. With them a user can make an elevated
    // .NET program load a runtime, a profiler, a startup hook or extra assemblies of the user,
    // or write traces and dumps where the user wants.
    private static readonly string[] RuntimeVariablePrefixes =
    [
        "DOTNET_",
        "COREHOST_",
        "CORECLR_",
        "COR_",
        "COMPlus_",
    ];

    // The prefixes already cover these names; they are listed so that a change of the prefixes
    // cannot let the runtime location variables through.
    private static readonly string[] RuntimeVariableNames =
    [
        "DOTNET_ROOT",
        "DOTNET_ROOT(x86)",
        "DOTNET_ROOT_X64",
    ];

    /// <summary>
    /// Gets the file name of the target <paramref name="name"/>. The name must be equal to an
    /// allowed name (ordinal comparison).
    /// </summary>
    public static bool TryGetTargetFileName(string name, out string fileName)
    {
        foreach (KeyValuePair<string, string> target in Targets)
        {
            if (string.Equals(target.Key, name, StringComparison.Ordinal))
            {
                fileName = target.Value;
                return true;
            }
        }
        fileName = string.Empty;
        return false;
    }

    /// <summary>
    /// The tray keeps running, so the launcher does not wait for it. The GUI reads the exit code
    /// of EditHostFile.exe, so the launcher waits for it and returns its exit code.
    /// </summary>
    public static bool WaitsForExit(string name)
    {
        return string.Equals(name, HostsTarget, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true for a variable that the .NET host or the runtime reads. Windows compares
    /// variable names without case, so the comparison ignores case.
    /// </summary>
    public static bool IsRuntimeVariable(string name)
    {
        foreach (string prefix in RuntimeVariablePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (string exactName in RuntimeVariableNames)
        {
            if (string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Removes each runtime variable from <paramref name="environment"/> and keeps the others.
    /// </summary>
    public static void RemoveRuntimeVariables(IDictionary<string, string?> environment)
    {
        var names = new List<string>(environment.Keys);
        foreach (string name in names)
        {
            if (IsRuntimeVariable(name))
                environment.Remove(name);
        }
    }
}
