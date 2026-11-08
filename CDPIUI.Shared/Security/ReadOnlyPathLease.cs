using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

#nullable enable

#if UPDATE_TOOL
namespace Update;
#else
namespace CDPIUI.Shared.Security;
#endif

/// <summary>Windows handles that reject links and hold a file and its parents against mutation.</summary>
public sealed class ReadOnlyPathLease : IDisposable
{
    private readonly List<SafeFileHandle> _directories = new();
    public FileStream? Stream { get; private set; }

    private ReadOnlyPathLease() { }

    public static ReadOnlyPathLease OpenDirectory(string path) => Open(path, directoryOnly: true);
    public static ReadOnlyPathLease OpenFile(string path) => Open(path, directoryOnly: false);

    private static ReadOnlyPathLease Open(string path, bool directoryOnly)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("A Windows file lease is required.");
        var lease = new ReadOnlyPathLease();
        try
        {
            string fullPath = Path.GetFullPath(path);
            string parent = directoryOnly ? fullPath : Path.GetDirectoryName(fullPath)!;
            var chain = new List<string>();
            for (string? current = parent; current != null; current = Path.GetDirectoryName(current))
                chain.Add(current);
            chain.Reverse();
            foreach (string directory in chain)
            {
                SafeFileHandle handle = OpenHandle(directory, 0x0081, directory: true);
                lease._directories.Add(handle);
                Information info = GetInformation(handle);
                if ((info.Attributes & 0x410) != 0x10)
                    throw new UnauthorizedAccessException($"{directory} is not an ordinary directory.");
            }
            if (!directoryOnly)
            {
                SafeFileHandle handle = OpenHandle(fullPath, 0x80000000, directory: false);
                try
                {
                    Information info = GetInformation(handle);
                    if ((info.Attributes & 0x410) != 0 || info.Links != 1)
                        throw new UnauthorizedAccessException($"{fullPath} is a link or is not an ordinary file.");
                    lease.Stream = new FileStream(handle, FileAccess.Read);
                }
                catch { handle.Dispose(); throw; }
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private static SafeFileHandle OpenHandle(string path, uint access, bool directory)
    {
        // FILE_LIST_DIRECTORY makes directory sharing restrictions effective. Attribute-only
        // handles do not prevent renames. OPEN_REPARSE_POINT inspects the opened object itself.
        SafeFileHandle handle = CreateFileW(path, access, 1, IntPtr.Zero, 3,
            0x00200000u | (directory ? 0x02000000u : 0u), IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetHRForLastWin32Error();
            handle.Dispose();
            throw new IOException($"Cannot hold {path}.", error);
        }
        return handle;
    }

    private static Information GetInformation(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out Information information))
            throw new IOException("Cannot inspect the held object.", Marshal.GetHRForLastWin32Error());
        return information;
    }

    public void Dispose()
    {
        Stream?.Dispose();
        Stream = null;
        for (int i = _directories.Count - 1; i >= 0; i--)
            _directories[i].Dispose();
        _directories.Clear();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Information
    {
        public uint Attributes;
        public uint CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information information);
}
