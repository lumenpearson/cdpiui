using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Shared.Security;

/// <summary>Holds an intentional file link or ordinary file for namespace operations only; never writes its contents.</summary>
public sealed class FileNameLease : IDisposable
{
    private ReadOnlyPathLease? _parent;
    private SafeFileHandle? _file;
    public string CurrentPath { get; private set; } = string.Empty;
    public bool IsReparsePoint { get; private set; }
    private FileNameLease() { }

    public static FileNameLease OpenExisting(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("A Windows file-name lease is required.");
        string full = Path.GetFullPath(path);
        WindowsPathNames.RequireOrdinaryAbsolutePath(full);
        var lease = new FileNameLease { CurrentPath = full };
        try
        {
            lease._parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(full)!, createIfMissing: false);
            lease._file = WindowsFileHandles.OpenRelative(lease._parent.DirectoryHandle, Path.GetFileName(full),
                0x80010080, 1, 1, false, out _, allowLeafReparse: true); // READ, DELETE, attributes; no data writes.
            if (!GetFileInformationByHandle(lease._file, out Information info))
                throw new IOException("Cannot inspect the held file name.", Marshal.GetHRForLastWin32Error());
            if ((info.Attributes & 0x10) != 0) throw new IOException("A file name, not a directory, is required.");
            lease.IsReparsePoint = (info.Attributes & 0x400) != 0;
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public void MoveTo(string destination)
    {
        if (_file == null) throw new ObjectDisposedException(nameof(FileNameLease));
        string full = Path.GetFullPath(destination);
        WindowsPathNames.RequireOrdinaryAbsolutePath(full);
        var target = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(full)!);
        bool transferred = false;
        try
        {
            WindowsFileHandles.Rename(_file, target.DirectoryHandle, Path.GetFileName(full), replace: false);
            CurrentPath = full;
            var previous = _parent;
            _parent = target;
            transferred = true;
            previous?.Dispose();
        }
        finally { if (!transferred) target.Dispose(); }
    }

    public void DeleteOnClose()
    {
        if (_file == null) throw new ObjectDisposedException(nameof(FileNameLease));
        byte[] disposition = { 1 };
        if (!SetFileInformationByHandle(_file, 4, disposition, 1))
            throw new IOException("Cannot mark the held file name for deletion.", Marshal.GetHRForLastWin32Error());
    }

    public void Dispose()
    {
        _file?.Dispose();
        _file = null;
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
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information information);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, byte[] information, uint size);
}
