using CDPIUI.Core.Security;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions.Catalog;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core.System
{
    internal class ZipService
    {
        public static async Task ExtractZip(
            string zipFilePath,
            string? zipFolderToUnpack,
            string extractTo,
            IEnumerable<string>? filesToSkip = null,
            bool isCatalogCheckRequired = false
        )
        {
            if (!isCatalogCheckRequired)
            {
                await ExtractEntries(zipFilePath, zipFolderToUnpack, extractTo, filesToSkip, mergeTextFiles: true);
                return;
            }

            // A signed archive is checked in a folder of its own and moved into place only when its
            // catalog is valid, so files of a rejected archive never reach the destination.
            string verifyDirectory = extractTo.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                ".verify-" + Guid.NewGuid().ToString("N");
            try
            {
                await ExtractEntries(zipFilePath, zipFolderToUnpack, verifyDirectory, filesToSkip, mergeTextFiles: false);
                await VerifyCatalog(verifyDirectory);
                MoveContents(verifyDirectory, extractTo);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(verifyDirectory))
                        Directory.Delete(verifyDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    Basic.Logger.Instance.CreateWarningLog(nameof(ZipService), $"Cannot delete the verification folder {verifyDirectory}: {ex.Message}");
                }
            }
        }

        private static void MoveContents(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(file, destination, overwrite: true);
            }
            foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        private static async Task ExtractEntries(
            string zipFilePath,
            string? zipFolderToUnpack,
            string extractTo,
            IEnumerable<string>? filesToSkip,
            bool mergeTextFiles
        )
        {
            filesToSkip = filesToSkip ?? Enumerable.Empty<string>();

            if (!Directory.Exists(extractTo))
                Directory.CreateDirectory(extractTo);

            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                var entries = archive.Entries;
                ArchivePaths.EnsureWithinLimits(entries);
                int totalFiles = entries.Count;
                int extractedFiles = 0;

                if (zipFolderToUnpack == null || zipFolderToUnpack == "/")
                    zipFolderToUnpack = string.Empty;
                else if (zipFolderToUnpack.EndsWith("/"))
                    zipFolderToUnpack = zipFolderToUnpack.TrimEnd('/');

                var patternSegments = string.IsNullOrEmpty(zipFolderToUnpack)
                                        ? Array.Empty<string>()
                                        : zipFolderToUnpack.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in entries)
                {
                    var entryPath = entry.FullName.Replace('\\', '/').TrimStart('/');

                    var entrySegments = entryPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                    bool isMatch = true;
                    if (patternSegments.Length > 0)
                    {
                        if (entrySegments.Length < patternSegments.Length)
                        {
                            isMatch = false;
                        }
                        else
                        {
                            for (int i = 0; i < patternSegments.Length; i++)
                            {
                                var pat = patternSegments[i];
                                var seg = entrySegments[i];

                                if (pat == "$ANY")
                                {
                                    continue;
                                }

                                if (!string.Equals(pat, seg, StringComparison.OrdinalIgnoreCase))
                                {
                                    isMatch = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (!isMatch)
                        continue;

                    var relativeSegments = entrySegments.Skip(patternSegments.Length).ToArray();
                    var relativePath = string.Join("/", relativeSegments).TrimStart('/');

                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    // "*.ext" skips by extension; any other value skips paths that contain it.
                    if (filesToSkip.Any(skip => skip.StartsWith("*.")
                            ? relativePath.EndsWith(skip.Substring(1), StringComparison.OrdinalIgnoreCase)
                            : relativePath.Contains(skip)))
                        continue;

                    var destinationPath = ArchivePaths.GetDestinationOrThrow(extractTo, relativePath);
                    ArchivePaths.EnsureNoReparsePoints(extractTo, destinationPath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir) && !string.IsNullOrEmpty(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    if (mergeTextFiles)
                    {
                        if (relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                            && File.Exists(destinationPath))
                        {
                            // This code is actually piece of shit. But, i so tired now to make something better. =(
                            if (File.Exists($"{destinationPath}.bak"))
                            {
                                destinationPath = $"{destinationPath}.bak";
                            }
                            // End of piece of shit
                            // Whole lines are compared: as a substring, "t.co" was dropped because
                            // the list already had "reddit.com".
                            var destLines = new HashSet<string>(
                                File.ReadLines(destinationPath).Select(line => line.Trim()), StringComparer.Ordinal);
                            bool endsWithNewLine;
                            using (var existing = File.OpenRead(destinationPath))
                            {
                                endsWithNewLine = existing.Length == 0;
                                if (!endsWithNewLine)
                                {
                                    existing.Seek(-1, SeekOrigin.End);
                                    endsWithNewLine = existing.ReadByte() == '\n';
                                }
                            }

                            var newLines = new List<string>();
                            using (var reader = new StreamReader(entry.Open()))
                            {
                                string? line;
                                while ((line = await reader.ReadLineAsync()) != null)
                                {
                                    if (line.Trim().Length > 0 && destLines.Add(line.Trim()))
                                        newLines.Add(line);
                                }
                            }

                            if (newLines.Count > 0)
                            {
                                using var stream = File.AppendText(destinationPath);
                                if (!endsWithNewLine)
                                    await stream.WriteLineAsync();
                                foreach (string line in newLines)
                                    await stream.WriteLineAsync(line);
                            }

                            continue;
                        }
                    }

                    if (entry.FullName.EndsWith("/"))
                    {
                        if (!Directory.Exists(destinationPath))
                            Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        ArchivePaths.ExtractToFileBounded(entry, destinationPath);
                    }

                    extractedFiles++;
                }
            }
        }

        private static async Task VerifyCatalog(string directory)
        {
            CatalogCheckResult catalogCheckResult = await CertificateCheck.CheckCatalog(Path.Combine(directory, "catalog.cat"), directory);
            switch (catalogCheckResult)
            {
                case CatalogCheckResult.Success:
                    return;
                case CatalogCheckResult.FailureNoSignature:
                    throw new CatalogNoSignature("Catalog file isn't signed");
                case CatalogCheckResult.FailureNotTrustedSignature:
                    throw new CatalogNoSignature("Signature not trusted");
                case CatalogCheckResult.FailureNotValid:
                    throw new CatalogInvalid();
                default:
                    throw new CatalogInvalid("Unknown");
            }
        }
    }
}
