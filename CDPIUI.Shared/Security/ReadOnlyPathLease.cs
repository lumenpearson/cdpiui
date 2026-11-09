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

/// <summary>Windows file leases with handle-relative traversal. Empty directory types are checked at child operations.</summary>
public sealed class ReadOnlyPathLease : IDisposable
{
    private readonly List<SafeFileHandle> _directories = new();
    private readonly object _gate = new();
    private string? _directoryPath;
    private ReadOnlyPathLease? _parent;
    private FileStream? _anchor;
    private int _dependents;
    private bool _disposed;
    public FileStream? Stream { get; private set; }

    private ReadOnlyPathLease() { }
    internal SafeFileHandle DirectoryHandle => _directories.Count > 0 ? _directories[_directories.Count - 1] : throw new ObjectDisposedException(nameof(ReadOnlyPathLease));

    public static ReadOnlyPathLease OpenDirectory(string path) => Open(path, directoryOnly: true);
    public static ReadOnlyPathLease OpenFile(string path) => Open(path, directoryOnly: false);
    public static ReadOnlyPathLease OpenDirectoryForMutation(string path, bool createIfMissing = true)
    {
        var lease = createIfMissing ? OpenOrCreateDirectory(path) : OpenDirectory(path);
        try
        {
            // A pinned child keeps the directory nonempty while namespace changes are
            // allowed. Create relative to the held object, never through its mutable name.
            WindowsFileHandles.RequireOrdinaryDirectory(lease.DirectoryHandle);
            SafeFileHandle anchor = WindowsFileHandles.OpenRelative(lease.DirectoryHandle,
                ".cdpiui-directory-lease-" + Guid.NewGuid().ToString("N"), 0xC0010000, 0, 2, false, out _, deleteOnClose: true);
            try
            {
                WindowsFileHandles.RequireOrdinaryDirectory(lease.DirectoryHandle);
                lease._anchor = new FileStream(anchor, FileAccess.ReadWrite);
            }
            catch { anchor.Dispose(); throw; }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
    public static ReadOnlyPathLease OpenOrCreateDirectory(string path) => Open(path, directoryOnly: true, createDirectories: true);
    public static ReadOnlyPathLease OpenFileInHeldDirectory(string path, ReadOnlyPathLease parent) => OpenChild(path, parent, false);
    public static ReadOnlyPathLease OpenDirectoryInHeldDirectory(string path, ReadOnlyPathLease parent) => OpenChild(path, parent, true);

    private static ReadOnlyPathLease OpenChild(string path, ReadOnlyPathLease parent, bool directory)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
        lock (parent._gate)
        {
            if (parent._disposed) throw new ObjectDisposedException(nameof(parent));
            if (parent._directoryPath == null || !string.Equals(Path.GetDirectoryName(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                parent._directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The path must be an immediate child of the held directory.", nameof(path));
            parent._dependents++;
        }
        var lease = new ReadOnlyPathLease { _parent = parent };
        try
        {
            lease.OpenLeaf(fullPath, directory, parent.DirectoryHandle);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private static ReadOnlyPathLease Open(string path, bool directoryOnly, bool createDirectories = false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("A Windows file lease is required.");
        var lease = new ReadOnlyPathLease();
        try
        {
            string fullPath = Path.GetFullPath(path);
            WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
            string parent = directoryOnly ? fullPath : Path.GetDirectoryName(fullPath)!;
            string root = Path.GetPathRoot(fullPath)!;
            SafeFileHandle rootHandle = OpenHandle(root, 0x0081, directory: true, share: 3);
            lease._directories.Add(rootHandle);
            WindowsFileHandles.RequireOrdinaryDirectory(rootHandle);
            string relative = Path.GetRelativePath(root, parent);
            string[] components = relative == "." ? Array.Empty<string>()
                : relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (components.Length > 256) throw new InvalidDataException("The directory chain is too deep.");
            foreach (string name in components)
            {
                SafeFileHandle heldParent = lease.DirectoryHandle;
                WindowsFileHandles.RequireOrdinaryDirectory(heldParent);
                SafeFileHandle handle = WindowsFileHandles.OpenRelative(heldParent, name, 0x0081, 3,
                    createDirectories ? 3u : 1u, true, out _);
                lease._directories.Add(handle);
                WindowsFileHandles.RequireOrdinaryDirectory(handle);
                WindowsFileHandles.RequireOrdinaryDirectory(heldParent);
            }
            if (directoryOnly) lease._directoryPath = fullPath;
            else lease.OpenLeaf(fullPath, false, lease.DirectoryHandle);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private void OpenLeaf(string fullPath, bool directory, SafeFileHandle parent)
    {
        WindowsFileHandles.RequireOrdinaryDirectory(parent);
        SafeFileHandle handle = WindowsFileHandles.OpenRelative(parent, Path.GetFileName(fullPath),
            directory ? 0x0081u : 0x80000000u, directory ? 3u : 1u, 1, directory, out _);
        try
        {
            WindowsFileHandles.RequireOrdinaryDirectory(parent);
            Information info = GetInformation(handle);
            if (directory ? (info.Attributes & 0x410) != 0x10 : (info.Attributes & 0x410) != 0 || info.Links != 1)
                throw new UnauthorizedAccessException($"{fullPath} is a link or is not an ordinary {(directory ? "directory" : "file")}.");
            if (directory)
            {
                _directories.Add(handle);
                _directoryPath = fullPath;
            }
            else Stream = new FileStream(handle, FileAccess.Read);
        }
        catch { handle.Dispose(); throw; }
    }

    private static SafeFileHandle OpenHandle(string path, uint access, bool directory, uint share = 1)
    {
        // FILE_LIST_DIRECTORY makes directory sharing restrictions effective. Attribute-only
        // handles do not prevent renames. OPEN_REPARSE_POINT inspects the opened object itself.
        SafeFileHandle handle = CreateFileW(path, access, share, IntPtr.Zero, 3,
            0x00200000u | (directory ? 0x02000000u : 0u), IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetHRForLastWin32Error();
            handle.Dispose();
            int code = error & 0xFFFF;
            if (code == 2 && !directory) throw new FileNotFoundException($"Cannot hold {path}.", path);
            if (code == 3 || (code == 2 && directory)) throw new DirectoryNotFoundException($"Cannot hold {path}.");
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
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_dependents == 0) CloseResources();
        }
    }

    private void ReleaseParentReference()
    {
        lock (_gate)
        {
            _dependents--;
            if (_disposed && _dependents == 0) CloseResources();
        }
    }

    private void CloseResources()
    {
        Stream?.Dispose();
        Stream = null;
        for (int i = _directories.Count - 1; i >= 0; i--)
            _directories[i].Dispose();
        _directories.Clear();
        _anchor?.Dispose();
        _anchor = null;
        _parent?.ReleaseParentReference();
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
