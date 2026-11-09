using System;
using System.Collections.Generic;
using System.IO;
#if UPDATE_TOOL
namespace Update;
#else
using CDPIUI.Shared.Basic.Filesystem;
namespace CDPIUI.Shared.Security;
#endif

/// <summary>A bounded, fixed set of ordinary files held against mutation during verification and use.</summary>
public sealed class ReadOnlyDirectorySnapshot : IDisposable
{
    public sealed class FileEntry
    {
        public string RelativePath { get; }
        public FileStream Stream { get; }
        internal FileEntry(string path, FileStream stream) { RelativePath = path; Stream = stream; }
    }
    private readonly List<FileEntry> _files = new();
    private readonly List<string> _directories = new();
    private readonly List<string> _skippedFiles = new();
    private readonly List<ReadOnlyPathLease> _leases = new();
    public IReadOnlyList<FileEntry> Files { get; }
    public IReadOnlyList<string> Directories { get; }
    /// <summary>Names excluded by the caller; their contents are neither opened nor verified.</summary>
    public IReadOnlyList<string> SkippedFiles { get; }
    public string RootPath { get; private set; } = string.Empty;

    private ReadOnlyDirectorySnapshot()
    {
        Files = _files.AsReadOnly();
        Directories = _directories.AsReadOnly();
        SkippedFiles = _skippedFiles.AsReadOnly();
    }

    public static ReadOnlyDirectorySnapshot Capture(string root) => Capture(root, _ => true);

    /// <summary>Captures included ordinary files, while traversing every directory without following links.</summary>
    public static ReadOnlyDirectorySnapshot Capture(string root, Func<string, bool> includeFile)
    {
        if (includeFile == null) throw new ArgumentNullException(nameof(includeFile));
        var snapshot = new ReadOnlyDirectorySnapshot();
        try
        {
            root = Path.GetFullPath(root);
            snapshot.RootPath = root;
            var rootLease = ReadOnlyPathLease.OpenDirectory(root);
            snapshot._leases.Add(rootLease);
            int count = 0;
            long bytes = 0;
            snapshot.Visit(root, "", rootLease, 0, includeFile, ref count, ref bytes);
            return snapshot;
        }
        catch { snapshot.Dispose(); throw; }
    }

    private void Visit(string path, string relative, ReadOnlyPathLease parent, int depth, Func<string, bool> includeFile, ref int count, ref long bytes)
    {
        // Do not use AllDirectories: a child must be opened without following links before
        // it is enumerated. Every directory needs only one handle, regardless of its depth.
        foreach (WindowsFileHandles.Entry item in WindowsFileHandles.Enumerate(parent.DirectoryHandle))
        {
            if (++count > ArchivePaths.MaxEntries || depth >= 64)
                throw new InvalidDataException("The verification tree exceeds its entry or depth limit.");
            string name = item.Name;
            string entry = Path.Combine(path, name);
            string childRelative = Path.Combine(relative, name);
            FileAttributes attributes = item.Attributes;
            // Exclusions apply only to files, never to a directory subtree. Intentional
            // data-file links may be skipped without opening the link or its target.
            if ((attributes & FileAttributes.Directory) == 0 && !includeFile(childRelative))
            {
                _skippedFiles.Add(childRelative);
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("The verification tree contains a link.");
            if ((attributes & FileAttributes.Directory) != 0)
            {
                var child = ReadOnlyPathLease.OpenDirectoryInHeldDirectory(entry, parent);
                _leases.Add(child);
                _directories.Add(childRelative);
                Visit(entry, childRelative, child, depth + 1, includeFile, ref count, ref bytes);
            }
            else
            {
                var file = ReadOnlyPathLease.OpenFileInHeldDirectory(entry, parent);
                _leases.Add(file);
                bytes = checked(bytes + file.Stream!.Length);
                if (bytes > ArchivePaths.MaxTotalBytes)
                    throw new InvalidDataException("The verification tree contains more data than allowed.");
                _files.Add(new FileEntry(childRelative, file.Stream));
            }
        }
    }

    public void Dispose()
    {
        for (int i = _leases.Count - 1; i >= 0; i--) _leases[i].Dispose();
        _leases.Clear();
    }
}
