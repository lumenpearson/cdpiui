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

/// <summary>Bounded no-follow removal of a caller-owned tree or file name.</summary>
public static class WindowsFileRemoval
{
    // Independent metadata/handle budgets; no file contents are read during removal.
    private const int MaxEntries = 20000, MaxDepth = 64;
    private sealed class Entry
    {
        public SafeFileHandle Handle { get; }
        public bool Directory { get; }
        public Entry(SafeFileHandle handle, bool directory) { Handle = handle; Directory = directory; }
    }

    public static bool DeleteDirectoryIfExists(string path) => DeleteDirectory(path, requireEmpty: false);
    public static bool DeleteEmptyDirectoryIfExists(string path) => DeleteDirectory(path, requireEmpty: true);

    internal static void DeleteHeldDirectory(SafeFileHandle root, string ownAnchorName, Action closeOwnAnchor)
    {
        var entries = new List<Entry>();
        try
        {
            WindowsFileHandles.RequireOrdinaryDirectory(root);
            int count = 0;
            Capture(root, 0, entries, ref count, ownAnchorName);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Directory) WindowsFileHandles.RequireOrdinaryDirectory(entries[i].Handle);
                MarkForDeletion(entries[i].Handle);
                entries[i].Handle.Dispose();
            }
            closeOwnAnchor();
            WindowsFileHandles.RequireOrdinaryDirectory(root);
            MarkForDeletion(root);
        }
        finally { for (int i = entries.Count - 1; i >= 0; i--) entries[i].Handle.Dispose(); }
    }

    private static bool DeleteDirectory(string path, bool requireEmpty)
    {
        string full = CheckedPath(path);
        using ReadOnlyPathLease? parent = TryOpenParent(full);
        if (parent == null) return false;
        var entries = new List<Entry>();
        try
        {
            SafeFileHandle? root = TryOpen(parent.DirectoryHandle, Path.GetFileName(full), directory: true);
            if (root == null) return false;
            entries.Add(new Entry(root, true));
            WindowsFileHandles.RequireOrdinaryDirectory(root);
            if (requireEmpty)
            {
                using var children = WindowsFileHandles.Enumerate(root).GetEnumerator();
                if (children.MoveNext()) throw new IOException("The recovery directory is not empty.", unchecked((int)0x80070091));
            }
            else
            {
                int count = 0;
                Capture(root, 0, entries, ref count);
            }
            // No namespace changes happen until the entire bounded preflight succeeds.
            // Late entries make the directory removal fail, rather than start a new walk.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                if (entry.Directory) WindowsFileHandles.RequireOrdinaryDirectory(entry.Handle);
                MarkForDeletion(entry.Handle);
                entry.Handle.Dispose();
            }
            return true;
        }
        finally
        {
            for (int i = entries.Count - 1; i >= 0; i--) entries[i].Handle.Dispose();
        }
    }

    public static bool DeleteFileIfExists(string path)
    {
        string full = CheckedPath(path);
        using ReadOnlyPathLease? parent = TryOpenParent(full);
        if (parent == null) return false;
        using SafeFileHandle? file = TryOpen(parent.DirectoryHandle, Path.GetFileName(full), directory: false);
        if (file == null) return false;
        if ((WindowsFileHandles.GetAttributes(file) & FileAttributes.Directory) != 0)
            throw new UnauthorizedAccessException("File removal cannot remove a directory.");
        MarkForDeletion(file);
        return true;
    }

    private static void Capture(SafeFileHandle directory, int depth, List<Entry> entries, ref int count, string? ignoredRootName = null)
    {
        foreach (WindowsFileHandles.Entry item in WindowsFileHandles.Enumerate(directory))
        {
            if (depth == 0 && string.Equals(item.Name, ignoredRootName, StringComparison.OrdinalIgnoreCase)) continue;
            if (++count > MaxEntries || depth >= MaxDepth) throw new InvalidDataException("The removal tree exceeds its entry or depth limit.");
            bool isDirectory = (item.Attributes & FileAttributes.Directory) != 0;
            if (isDirectory && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("The removal tree contains a directory link.");
            WindowsFileHandles.RequireOrdinaryDirectory(directory);
            SafeFileHandle child = WindowsFileHandles.OpenRelative(directory, item.Name,
                isDirectory ? 0x10081u : 0x10080u, 3, 1, isDirectory, out _);
            entries.Add(new Entry(child, isDirectory));
            WindowsFileHandles.RequireOrdinaryDirectory(directory);
            if (isDirectory)
            {
                WindowsFileHandles.RequireOrdinaryDirectory(child);
                Capture(child, depth + 1, entries, ref count);
            }
            else if ((WindowsFileHandles.GetAttributes(child) & FileAttributes.Directory) != 0)
                throw new UnauthorizedAccessException("A captured file changed its type.");
        }
    }

    private static string CheckedPath(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("Windows removal handles are required.");
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full)!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Removing a volume or share root is forbidden.");
        WindowsPathNames.RequireOrdinaryAbsolutePath(full);
        return full;
    }

    private static ReadOnlyPathLease? TryOpenParent(string full)
    {
        try { return ReadOnlyPathLease.OpenDirectoryForMutation(Path.GetDirectoryName(full)!, createIfMissing: false); }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static SafeFileHandle? TryOpen(SafeFileHandle parent, string name, bool directory)
    {
        WindowsFileHandles.RequireOrdinaryDirectory(parent);
        try { return WindowsFileHandles.OpenRelative(parent, name, directory ? 0x10081u : 0x10080u, 3, 1, directory, out _); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void MarkForDeletion(SafeFileHandle handle)
    {
        byte[] disposition = { 1 };
        if (!SetFileInformationByHandle(handle, 4, disposition, 1))
            throw new IOException("Cannot delete the held object.", Marshal.GetHRForLastWin32Error());
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, byte[] information, uint length);
}
