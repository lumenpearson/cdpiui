using System.Diagnostics;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Security;

namespace CDPIUI.AddOns.HostsFileEditor;

public class HostsFileEditService
{
    private enum Flags
    {
        add,
        remove,
        recover,
    }
    private static Process? RunEditorWithFlag(Flags flag)
    {
        string startupString = $"/{flag}";
        string directory = Core.Data.Directories.CurrentDirectory;
        string path = Path.Combine(directory, "EditHostFile.exe");
        string launcherPath = ElevatedLauncherCommand.GetPath(directory);

        if (!Path.Exists(path) || !Path.Exists(launcherPath))
            throw new ApplicationFilesDamagedException("File not found");

        // The launcher removes the .NET variables of the user profile, starts the tool and
        // returns its exit code.
        var psi = new ProcessStartInfo(launcherPath,
            ElevatedLauncherCommand.GetArguments(ElevatedLauncherCommand.HostsTarget, startupString))
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        var process = Process.Start(psi);
        return process;
    }

    public static Task<int> AddDomains() => RunAndWait(Flags.add);

    public static Task<int> RemoveDomains() => RunAndWait(Flags.remove);

    public static Task<int> RestoreDomains() => RunAndWait(Flags.recover);

    // Process.Start can return null; the callers then failed with NullReferenceException.
    private static async Task<int> RunAndWait(Flags flag)
    {
        using Process? process = RunEditorWithFlag(flag);
        if (process == null)
            return -1;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
