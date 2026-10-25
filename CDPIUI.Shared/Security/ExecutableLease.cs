using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Shared.Security;

/// <summary>
/// Read handles on a program and the code files of its folder. While they are open, nobody can
/// change, rename or delete these files, and nobody can rename the folders above them.
/// </summary>
public sealed class ExecutableLease : IDisposable
{
    // Files that run as code: the ones Windows loads from the folder of the program, the Lua
    // scripts of Zapret2 (--lua-init and the files they load) and scripts of Store items.
    public static readonly IReadOnlyList<string> CodeExtensions = new[]
    {
        ".exe", ".dll", ".sys", ".lua", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".com"
    };

    // A Store item has a few dozen files; far more means something else is in the folder.
    private const int MaxCodeFiles = 5000;

    private readonly List<(string Path, FileStream Stream)> _files;
    // Files the arguments refer to: locked like the code files, but not part of the fingerprint.
    private readonly List<FileStream> _argumentFiles = new();
    private readonly List<SafeFileHandle> _directories = new();

    private ExecutableLease(string root, List<(string Path, FileStream Stream)> files)
    {
        Root = root;
        _files = files;
    }

    /// <summary>A lease that locks nothing, for a portable copy. Dispose it like any other.</summary>
    public static ExecutableLease CreateEmpty() => new ExecutableLease(string.Empty, new List<(string, FileStream)>());

    /// <summary>The folder whose code files the lease holds: the Store item folder, or the folder of the program.</summary>
    public string Root { get; }

    public IReadOnlyList<string> Paths => _files.Select(file => file.Path).ToList();

    /// <summary>
    /// Locks the program and every code file of its Store item folder (or of its own folder),
    /// subfolders included. A link inside that folder is rejected: the lock would not hold the
    /// file it points to.
    /// </summary>
    public static ExecutableLease Open(string executable)
    {
        string fullPath = Path.GetFullPath(executable);
        string root = ExecutableApprovals.GetStoreItemDirectory(fullPath) ?? Path.GetDirectoryName(fullPath)!;
        return OpenRoot(root, fullPath);
    }

    /// <summary>
    /// Locks every code file of a folder, subfolders included: a component folder that another
    /// program (GoodCheck) starts a program from.
    /// </summary>
    public static ExecutableLease OpenFolder(string folder) =>
        OpenRoot(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), program: null);

    private static ExecutableLease OpenRoot(string root, string? program)
    {
        var paths = new List<string>();
        if (program != null)
            paths.Add(program);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = 0,
        };
        foreach (string entry in Directory.EnumerateFileSystemEntries(root, "*", options))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"The folder of the program contains a link: {entry}");
            if (CodeExtensions.Contains(Path.GetExtension(entry), StringComparer.OrdinalIgnoreCase) && File.Exists(entry))
                paths.Add(Path.GetFullPath(entry));
            if (paths.Count > MaxCodeFiles)
                throw new IOException($"The folder of the program has more than {MaxCodeFiles} code files.");
        }
        paths = paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var files = new List<(string, FileStream)>();
        try
        {
            foreach (string path in paths)
                files.Add((path, new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)));
        }
        catch
        {
            foreach (var (_, stream) in files)
                stream.Dispose();
            throw;
        }
        return new ExecutableLease(root, files);
    }

    public bool Holds(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return _files.Any(file => string.Equals(file.Path, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    public string GetHash(string path)
    {
        string fullPath = Path.GetFullPath(path);
        foreach (var (filePath, stream) in _files)
        {
            if (string.Equals(filePath, fullPath, StringComparison.OrdinalIgnoreCase))
                return Hash(stream);
        }
        return string.Empty;
    }

    /// <summary>
    /// One hash over the paths (relative to <see cref="Root"/>) and contents of all locked code files.
    /// </summary>
    public string ComputeFingerprint()
    {
        var builder = new StringBuilder();
        foreach (var (path, stream) in _files)
            builder.Append(RelativeName(path)).Append(':').Append(Hash(stream)).Append('\n');
        return HashText(builder.ToString());
    }

    private string RelativeName(string path)
    {
        string name = Root.Length > 0 && path.StartsWith(Root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
            ? path.Substring(Root.TrimEnd('\\').Length + 1)
            : Path.GetFileName(path);
        return name.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>
    /// Locks a file the arguments refer to (an argument file, a Lua script) until the lease is
    /// disposed, and returns its contents read through the locked handle.
    /// </summary>
    public byte[] LockAndRead(string path, long maxBytes)
    {
        var stream = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (stream.Length > maxBytes)
                throw new InvalidDataException($"{path} is larger than {maxBytes} bytes.");
            var content = new byte[stream.Length];
            int read = 0;
            while (read < content.Length)
            {
                int count = stream.Read(content, read, content.Length - read);
                if (count == 0)
                    break;
                read += count;
            }
            _argumentFiles.Add(stream);
            return content;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Reads an argument file as text through a locked handle; for <see cref="FileWritingOptions"/>.</summary>
    public string ReadArgumentFile(string path) =>
        DecodeText(LockAndRead(path, FileWritingOptions.MaxArgumentFileSize));

    /// <summary>
    /// Holds the folders from the allowed root down to the folder of a file the component writes,
    /// so none of them can be renamed, deleted or turned into a link while the component runs.
    /// An existing file must not be a link or have a second name (hard link).
    /// </summary>
    public void GuardWriteTarget(string target, IEnumerable<string?> allowedRoots)
    {
        string fullPath = Path.GetFullPath(target);
        string? root = allowedRoots
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Path.GetFullPath(candidate!).TrimEnd('\\'))
            .Where(candidate => fullPath.StartsWith(candidate + "\\", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        if (root == null)
            throw new UnauthorizedAccessException($"{fullPath} is outside the application directories.");

        string parent = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(parent))
            throw new UnauthorizedAccessException($"The folder of {fullPath} does not exist.");

        var chain = new List<string>();
        for (string? current = parent; current != null; current = Path.GetDirectoryName(current))
        {
            chain.Add(current);
            if (string.Equals(current.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase))
                break;
        }
        chain.Reverse();
        foreach (string directory in chain)
        {
            SafeFileHandle handle = NativeMethods.OpenDirectory(directory);
            _directories.Add(handle);
            if (NativeMethods.IsReparsePoint(handle))
                throw new UnauthorizedAccessException($"{directory} is a link.");
        }

        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException($"{fullPath} is a link.");
            using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (NativeMethods.GetLinkCount(file.SafeFileHandle) > 1)
                throw new UnauthorizedAccessException($"{fullPath} has more than one name (hard link).");
        }
    }

    public static string HashText(string text)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    public static string HashBytes(byte[] content)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(content));
    }

    private static string DecodeText(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string Hash(FileStream stream)
    {
        stream.Position = 0;
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(stream));
    }

    private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);

    public void Dispose()
    {
        foreach (var (_, stream) in _files)
            stream.Dispose();
        _files.Clear();
        foreach (FileStream stream in _argumentFiles)
            stream.Dispose();
        _argumentFiles.Clear();
        foreach (SafeFileHandle handle in _directories)
            handle.Dispose();
        _directories.Clear();
    }

    private static class NativeMethods
    {
        private const uint FileReadAttributes = 0x0080;
        // A handle with attribute access only is exempt from share checks: without data access
        // (listing the folder) its share mode would not stop a rename.
        private const uint FileListDirectory = 0x0001;
        private const uint FileShareRead = 0x00000001;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileAttributeReparsePoint = 0x00000400;

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public long CreationTime;
            public long LastAccessTime;
            public long LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

        // Only read sharing: while the handle is open nobody can delete or rename the folder, or
        // open it for writing, which setting a reparse point needs.
        public static SafeFileHandle OpenDirectory(string path)
        {
            SafeFileHandle handle = CreateFileW(
                path, FileListDirectory | FileReadAttributes, FileShareRead, IntPtr.Zero, OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                // Read before Dispose, which can change the last error.
                int hresult = Marshal.GetHRForLastWin32Error();
                handle.Dispose();
                throw new IOException($"Cannot hold the folder {path}.", hresult);
            }
            return handle;
        }

        public static bool IsReparsePoint(SafeFileHandle handle) =>
            (GetInformation(handle).FileAttributes & FileAttributeReparsePoint) != 0;

        public static uint GetLinkCount(SafeFileHandle handle) => GetInformation(handle).NumberOfLinks;

        private static ByHandleFileInformation GetInformation(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
                throw new IOException("Cannot read the file information.", Marshal.GetHRForLastWin32Error());
            return information;
        }
    }
}

/// <summary>
/// The fingerprints of programs that may start with administrator rights, and of Lua code the
/// arguments give them. One line per entry: the fingerprint, a tab and the full path of the
/// program, or <c>code:</c> and the hash of the code. The tray keeps the file in a folder that
/// only administrators can change.
/// </summary>
public static class ExecutableApprovals
{
    public const string CodePrefix = "code:";

    // Approved code is kept without a program to check it against; the oldest go first.
    private const int MaxCodeEntries = 200;

    public static Dictionary<string, string> Load(string file)
    {
        var approved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(file))
            return approved;
        foreach (string line in File.ReadAllLines(file))
        {
            int tab = line.IndexOf('\t');
            if (tab <= 0 || tab == line.Length - 1)
                continue;
            approved[line.Substring(tab + 1)] = line.Substring(0, tab);
        }
        return approved;
    }

    /// <summary>
    /// Saves the approvals. Programs and folders that no longer exist are dropped, so the file does not grow without limit.
    /// </summary>
    public static void Save(string file, IReadOnlyDictionary<string, string> approved)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var programs = approved
            .Where(pair => !IsCode(pair.Key) && (File.Exists(pair.Key) || Directory.Exists(pair.Key)) && pair.Key.IndexOf('\n') < 0)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase);
        var code = approved
            .Where(pair => IsCode(pair.Key))
            .Reverse()
            .Take(MaxCodeEntries)
            .Reverse();
        var lines = programs.Concat(code).Select(pair => pair.Value + "\t" + pair.Key);
        string temporary = file + ".tmp";
        File.WriteAllLines(temporary, lines);
        if (File.Exists(file))
            File.Replace(temporary, file, null);
        else
            File.Move(temporary, file);
    }

    public static string CodeKey(string hash) => CodePrefix + hash;

    private static bool IsCode(string key) => key.StartsWith(CodePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The folder of the Store item that contains the program: <c>&lt;data&gt;\Store\Items\&lt;item id&gt;</c>, or null.
    /// </summary>
    public static string? GetStoreItemDirectory(string program)
    {
        string? current = Path.GetDirectoryName(Path.GetFullPath(program));
        while (current != null)
        {
            string? parent = Path.GetDirectoryName(current);
            if (parent != null &&
                string.Equals(Path.GetFileName(parent), "Items", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(Path.GetDirectoryName(parent)), "Store", StringComparison.OrdinalIgnoreCase))
                return current;
            current = parent;
        }
        return null;
    }
}
