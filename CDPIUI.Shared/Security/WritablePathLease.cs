using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
    private bool _moveAllowed;
    public string CurrentPath { get; private set; } = string.Empty;
    public FileStream Stream { get; private set; } = null!;
    private WritablePathLease() { }
    public static WritablePathLease CreateForMove(string path) => OpenNative(path, 1, movable: true, writable: true);
    internal static WritablePathLease CreateForMove(string path, byte[]? securityDescriptor) => OpenNative(path, 1, movable: true, writable: true, securityDescriptor);
    public static WritablePathLease OpenExistingForMove(string path) => OpenNative(path, 3, movable: true, writable: false);
    public static WritablePathLease OpenLock(string path)
    {
        var lease = OpenNative(path, 4, movable: true, writable: true);
        try
        {
            if (lease.Stream.Length != 0) throw new UnauthorizedAccessException("A lock must be an ordinary empty file.");
            // Inspect before setting disposition: an unsafe pre-existing object must not
            // be deleted as a side effect of rejecting it.
            lease.DeleteOnClose();
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public static WritablePathLease Open(string path) => OpenNative(path, 4, movable: false, writable: true);

    private static WritablePathLease OpenNative(string path, uint disposition, bool movable, bool writable, byte[]? securityDescriptor = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("A Windows write lease is required.");
        string fullPath = Path.GetFullPath(path);
        WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
        var lease = new WritablePathLease { _moveAllowed = movable, CurrentPath = fullPath };
        try
        {
            lease._parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(fullPath)!, createIfMissing: disposition != 3);
            // OPEN_ALWAYS preserves existing bytes. The opened object is inspected before
            // the caller can truncate it; OPEN_REPARSE_POINT never follows the leaf link.
            uint access = 0x80000000u | (writable ? 0x40000000u : 0u) | (movable ? 0x00010000u : 0u);
            WindowsFileHandles.RequireOrdinaryDirectory(lease._parent.DirectoryHandle);
            uint nativeDisposition = disposition == 1 ? 2u : disposition == 3 ? 1u : 3u;
            SafeFileHandle handle = WindowsFileHandles.OpenRelative(lease._parent.DirectoryHandle,
                Path.GetFileName(fullPath), access, 0, nativeDisposition, false, out _, securityDescriptor: securityDescriptor);
            try
            {
                WindowsFileHandles.RequireOrdinaryDirectory(lease._parent.DirectoryHandle);
                if (!GetFileInformationByHandle(handle, out Information info))
                    throw new IOException("Cannot inspect the write target.", Marshal.GetHRForLastWin32Error());
                if ((info.Attributes & 0x410) != 0 || info.Links != 1)
                    throw new UnauthorizedAccessException("The write target is a link or is not an ordinary file.");
                lease.Stream = new FileStream(handle, writable ? FileAccess.ReadWrite : FileAccess.Read);
            }
            catch { handle.Dispose(); throw; }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public void MoveTo(string destination, bool replace = false)
    {
        if (_parent == null) throw new ObjectDisposedException(nameof(WritablePathLease));
        if (!_moveAllowed) throw new InvalidOperationException("The file was not opened with rename access.");
        string fullPath = Path.GetFullPath(destination);
        WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
        var target = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(fullPath)!);
        bool transferred = false;
        try
        {
            if (Stream.CanWrite) Stream.Flush(flushToDisk: true);
            // Resolve only one name relative to the held destination directory. The
            // anchor pins its ordinary type throughout the namespace mutation.
            WindowsFileHandles.RequireOrdinaryDirectory(target.DirectoryHandle);
            byte[] name = Encoding.Unicode.GetBytes(Path.GetFileName(fullPath));
            int rootOffset = IntPtr.Size == 8 ? 8 : 4;
            int lengthOffset = rootOffset + IntPtr.Size;
            int nameOffset = lengthOffset + 4;
            byte[] data = new byte[nameOffset + name.Length + 2];
            Array.Copy(BitConverter.GetBytes(replace ? 1u : 0u), 0, data, 0, 4);
            byte[] root = IntPtr.Size == 8 ? BitConverter.GetBytes(target.DirectoryHandle.DangerousGetHandle().ToInt64())
                : BitConverter.GetBytes(target.DirectoryHandle.DangerousGetHandle().ToInt32());
            Array.Copy(root, 0, data, rootOffset, root.Length);
            Array.Copy(BitConverter.GetBytes((uint)name.Length), 0, data, lengthOffset, 4);
            Array.Copy(name, 0, data, nameOffset, name.Length);
            WindowsFileHandles.Rename(Stream.SafeFileHandle, data);
            CurrentPath = fullPath;
            var previous = _parent;
            _parent = target;
            transferred = true;
            previous.Dispose();
        }
        finally { if (!transferred) target.Dispose(); }
    }

    public void DeleteOnClose()
    {
        if (_parent == null) throw new ObjectDisposedException(nameof(WritablePathLease));
        if (!_moveAllowed) throw new InvalidOperationException("The file was not opened with delete access.");
        byte[] disposition = { 1 };
        if (!SetFileInformationByHandle(Stream.SafeFileHandle, 4, disposition, 1))
            throw new IOException("Cannot mark the held file for deletion.", Marshal.GetHRForLastWin32Error());
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, byte[] information, uint size);
}
