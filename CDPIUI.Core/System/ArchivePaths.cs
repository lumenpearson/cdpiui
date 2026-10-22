using System;
using System.IO;

namespace CDPIUI.Core.System
{
    public static class ArchivePaths
    {
        public static bool TryGetDestination(string root, string relativePath, out string destination)
        {
            destination = string.Empty;
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
                return false;

            string fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
                fullRoot += Path.DirectorySeparatorChar;

            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            destination = fullPath;
            return true;
        }

        public static string GetDestinationOrThrow(string root, string relativePath)
        {
            if (!TryGetDestination(root, relativePath, out string destination))
                throw new InvalidDataException($"Archive entry \"{relativePath}\" points outside of the target directory.");
            return destination;
        }
    }
}
