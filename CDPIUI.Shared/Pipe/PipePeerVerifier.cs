using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace CDPIUI.Shared.Pipe
{
    public static class PipePeerVerifier
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public static bool IsClientExpected(NamedPipeServerStream pipe, string expectedImagePath) =>
            GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint processId) &&
            IsProcessImage(processId, expectedImagePath);

        public static bool IsServerExpected(NamedPipeClientStream pipe, string expectedImagePath) =>
            GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId) &&
            IsProcessImage(processId, expectedImagePath);

        public static bool IsProcessImage(uint processId, string expectedImagePath)
        {
            string? imagePath = GetProcessImagePath(processId);
            if (imagePath == null)
                return false;

#if DEBUG
            // Debug builds of the UI and the tray are produced in different output folders.
            return string.Equals(Path.GetFileName(imagePath), Path.GetFileName(expectedImagePath), StringComparison.OrdinalIgnoreCase);
#else
            return string.Equals(Path.GetFullPath(imagePath), Path.GetFullPath(expectedImagePath), StringComparison.OrdinalIgnoreCase);
#endif
        }

        public static string? GetProcessImagePath(uint processId)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero)
                return null;

            try
            {
                var buffer = new StringBuilder(32768);
                int size = buffer.Capacity;
                return QueryFullProcessImageName(process, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(SafeHandle pipe, out uint clientProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeServerProcessId(SafeHandle pipe, out uint serverProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
