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
    private readonly object _gate = new();
    private string? _directoryPath;
    private ReadOnlyPathLease? _parent;
    private FileStream? _anchor;
    private int _dependents;
    private bool _disposed;
    public FileStream? Stream { get; private set; }

    private ReadOnlyPathLease() { }

    public static ReadOnlyPathLease OpenDirectory(string path) => Open(path, directoryOnly: true);
    public static ReadOnlyPathLease OpenFile(string path) => Open(path, directoryOnly: false);
    public static ReadOnlyPathLease OpenDirectoryForMutation(string path, bool createIfMissing = true)
    {
        var lease = createIfMissing ? OpenOrCreateDirectory(path) : OpenDirectory(path);
        try
        {
            string fullPath = Path.GetFullPath(path);
            // A pinned child keeps the directory nonempty while namespace changes are
            // allowed. DeleteOnClose cleans up this exact object on normal exit or crash.
            lease._anchor = new FileStream(Path.Combine(fullPath, ".cdpiui-directory-lease-" + Guid.NewGuid().ToString("N")),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            SafeFileHandle replacement = OpenHandle(fullPath, 0x0081, directory: true, share: 3);
            int leaf = lease._directories.Count - 1;
            lease._directories[leaf].Dispose();
            lease._directories[leaf] = replacement;
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
    public static ReadOnlyPathLease OpenOrCreateDirectory(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("A Windows directory lease is required.");
        string current = Path.GetFullPath(path);
        WindowsPathNames.RequireOrdinaryAbsolutePath(current);
        var missing = new Stack<string>();
        while (!Directory.Exists(current))
        {
            if (missing.Count >= 64) throw new InvalidDataException("Too many missing parent directories.");
            missing.Push(current);
            current = Path.GetDirectoryName(current) ?? throw new DirectoryNotFoundException(path);
        }
        ReadOnlyPathLease lease = OpenDirectory(current);
        try
        {
            while (missing.Count > 0)
            {
                string next = missing.Pop();
                // Only one immediate child is created while its ordinary parent is held.
                // If another process supplied that child as a link, the next open rejects it.
                Directory.CreateDirectory(next);
                var child = OpenDirectory(next);
                lease.Dispose();
                lease = child;
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }
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
            lease.OpenLeaf(fullPath, directory);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private static ReadOnlyPathLease Open(string path, bool directoryOnly)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("A Windows file lease is required.");
        var lease = new ReadOnlyPathLease();
        try
        {
            string fullPath = Path.GetFullPath(path);
            WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
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
            if (directoryOnly) lease._directoryPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            else lease.OpenLeaf(fullPath, false);
            // A deny-write directory handle also prevents renaming unrelated children.
            // Keep that restriction only on the leaf directory. Every ancestor is pinned
            // by its held child, so it cannot become empty (a prerequisite for setting a
            // directory reparse point). Deny-delete still prevents replacing the ancestor.
            for (int i = 0; i < chain.Count - 1; i++)
            {
                SafeFileHandle replacement = OpenHandle(chain[i], 0x0081, directory: true, share: 3);
                lease._directories[i].Dispose();
                lease._directories[i] = replacement;
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private void OpenLeaf(string fullPath, bool directory)
    {
        SafeFileHandle handle = OpenHandle(fullPath, directory ? 0x0081u : 0x80000000u, directory);
        try
        {
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
