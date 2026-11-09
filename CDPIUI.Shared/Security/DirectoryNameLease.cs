using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Shared.Security;

/// <summary>Namespace-only ownership of an ordinary directory; child bytes are not frozen.</summary>
internal sealed class DirectoryNameLease : IDisposable
{
    private ReadOnlyPathLease? _parent;
    private SafeFileHandle? _directory;
    private FileStream? _anchor;
    private readonly string _anchorName = ".cdpiui-directory-lease-" + Guid.NewGuid().ToString("N");
    internal string CurrentPath { get; private set; }

    private DirectoryNameLease(string path) => CurrentPath = path;
    internal static DirectoryNameLease Open(string path)
    {
        string full = NormalizeNonRootPath(path);
        var lease = new DirectoryNameLease(full);
        try
        {
            lease._parent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(full)!, createIfMissing: false);
            lease._directory = WindowsFileHandles.OpenRelative(lease._parent.DirectoryHandle, Path.GetFileName(full), 0x10081, 3, 1, true, out _);
            lease.CreateAnchor();
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    internal void MoveTo(string path)
    {
        if (_directory == null) throw new ObjectDisposedException(nameof(DirectoryNameLease));
        string full = NormalizeNonRootPath(path);
        WindowsFileHandles.RequireOrdinaryDirectory(_directory);
        var targetParent = ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(full)!, createIfMissing: false);
        bool transferred = false;
        Exception? failure = null;
        try
        {
            // Windows refuses a directory rename with this child held without delete
            // sharing. During this gap we perform only exact-root namespace operations.
            CloseAnchor();
            WindowsFileHandles.Rename(_directory, targetParent.DirectoryHandle, Path.GetFileName(full), replace: false);
            CurrentPath = full;
            var previous = _parent;
            _parent = targetParent;
            transferred = true;
            previous!.Dispose();
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            if (!transferred) targetParent.Dispose();
            try { if (_anchor == null) CreateAnchor(); }
            catch (Exception ex)
            {
                var anchorFailure = new IOException("Cannot anchor the retained directory: " + CurrentPath, ex);
                if (failure != null) throw new AggregateException(failure, anchorFailure);
                throw anchorFailure;
            }
        }
    }

    internal void DeleteTree()
    {
        if (_directory == null) throw new ObjectDisposedException(nameof(DirectoryNameLease));
        if (_anchor == null) throw new IOException("The retained directory must be anchored before cleanup: " + CurrentPath);
        WindowsFileRemoval.DeleteHeldDirectory(_directory, _anchorName, CloseAnchor);
        Dispose();
    }

    private void CreateAnchor()
    {
        WindowsFileHandles.RequireOrdinaryDirectory(_directory!);
        var handle = WindowsFileHandles.OpenRelative(_directory!, _anchorName, 0xC0010000, 0, 2, false, out _, deleteOnClose: true);
        try
        {
            WindowsFileHandles.RequireOrdinaryDirectory(_directory!);
            _anchor = new FileStream(handle, FileAccess.ReadWrite);
        }
        catch { handle.Dispose(); throw; }
    }

    internal static string NormalizeNonRootPath(string path)
    {
        string absolute = Path.GetFullPath(path);
        WindowsPathNames.RequireOrdinaryAbsolutePath(absolute);
        string full = absolute.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(full, Path.GetPathRoot(absolute)!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A volume or share root cannot be moved or removed.");
        return full;
    }

    private void CloseAnchor() { _anchor?.Dispose(); _anchor = null; }
    public void Dispose()
    {
        try { CloseAnchor(); }
        finally
        {
            _directory?.Dispose();
            _directory = null;
            _parent?.Dispose();
            _parent = null;
        }
    }
}
