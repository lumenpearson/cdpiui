using CDPIUI.Shared.Exceptions;
using System.Diagnostics;

namespace CDPIUI.AddOns.HostsFileEditor
{
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
            string path = Path.Combine(Core.Data.Directories.CurrentDirectory,
                "EditHostFile.exe");

            if (!Path.Exists(path)) throw new ApplicationFilesDamagedException("File not found");

            var psi = new ProcessStartInfo(path, startupString)
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
}
