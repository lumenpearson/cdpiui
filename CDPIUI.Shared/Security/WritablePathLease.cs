using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

#nullable enable
#if UPDATE_TOOL
namespace Update;
#else
namespace CDPIUI.Shared.Security;
#endif

/// <summary>A Windows no-follow read/write handle, checked before any truncation or write.</summary>
public sealed class WritablePathLease : IDisposable
{
    private ReadOnlyPathLease? _parent;
    public FileStream Stream { get; private set; } = null!;
    private WritablePathLease() { }

    public static WritablePathLease Open(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("A Windows write lease is required.");
        string fullPath = Path.GetFullPath(path);
        WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
        var lease = new WritablePathLease();
        try
        {
            lease._parent = ReadOnlyPathLease.OpenOrCreateDirectory(Path.GetDirectoryName(fullPath)!);
            // OPEN_ALWAYS preserves existing bytes. The opened object is inspected before
            // the caller can truncate it; OPEN_REPARSE_POINT never follows the leaf link.
            SafeFileHandle handle = CreateFileW(fullPath, 0xC0000000, 0, IntPtr.Zero, 4, 0x02200000, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetHRForLastWin32Error();
                handle.Dispose();
                throw new IOException($"Cannot open {fullPath} for writing.", error);
            }
            try
            {
                if (!GetFileInformationByHandle(handle, out Information info))
                    throw new IOException("Cannot inspect the write target.", Marshal.GetHRForLastWin32Error());
                if ((info.Attributes & 0x410) != 0 || info.Links != 1)
                    throw new UnauthorizedAccessException("The write target is a link or is not an ordinary file.");
                lease.Stream = new FileStream(handle, FileAccess.ReadWrite);
            }
            catch { handle.Dispose(); throw; }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public void Dispose()
    {
        Stream?.Dispose();
        _parent?.Dispose();
        _parent = null;
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
