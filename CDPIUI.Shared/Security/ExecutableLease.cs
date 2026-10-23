using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CDPIUI.Shared.Security
{
    /// <summary>
    /// Read handles on a program and the code files in its folder. While they are open, nobody can
    /// change, rename or delete these files, and nobody can rename the folders above them.
    /// </summary>
    public sealed class ExecutableLease : IDisposable
    {
        // Files that Windows loads as code from the folder of the program.
        public static readonly IReadOnlyList<string> CodeExtensions = new[] { ".exe", ".dll", ".sys" };

        public static readonly ExecutableLease None = new ExecutableLease(new List<(string, FileStream)>());

        private readonly List<(string Path, FileStream Stream)> _files;

        private ExecutableLease(List<(string Path, FileStream Stream)> files) => _files = files;

        public IReadOnlyList<string> Paths => _files.Select(file => file.Path).ToList();

        public static ExecutableLease Open(string executable)
        {
            string fullPath = Path.GetFullPath(executable);
            string directory = Path.GetDirectoryName(fullPath)!;
            var paths = Directory.EnumerateFiles(directory)
                .Where(file => CodeExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Append(fullPath)
                .Select(Path.GetFullPath)
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
            return new ExecutableLease(files);
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
        /// One hash over the names and contents of all locked files.
        /// </summary>
        public string ComputeFingerprint()
        {
            var builder = new StringBuilder();
            foreach (var (path, stream) in _files)
                builder.Append(Path.GetFileName(path).ToLowerInvariant()).Append(':').Append(Hash(stream)).Append('\n');
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
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
        }
    }

    /// <summary>
    /// The fingerprints of programs that may start with administrator rights. One line per program:
    /// the fingerprint, a tab and the full path. The tray keeps the file in a folder that only
    /// administrators can change.
    /// </summary>
    public static class ExecutableApprovals
    {
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
        /// Saves the approvals. Programs that no longer exist are dropped, so the file does not grow without limit.
        /// </summary>
        public static void Save(string file, IReadOnlyDictionary<string, string> approved)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            var lines = approved
                .Where(pair => File.Exists(pair.Key) && pair.Key.IndexOf('\n') < 0)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Value + "\t" + pair.Key);
            string temporary = file + ".tmp";
            File.WriteAllLines(temporary, lines);
            if (File.Exists(file))
                File.Replace(temporary, file, null);
            else
                File.Move(temporary, file);
        }

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
}
