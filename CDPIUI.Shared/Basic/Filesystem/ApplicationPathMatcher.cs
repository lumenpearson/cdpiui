using System;

namespace CDPIUI.Shared.Basic.Filesystem
{
    /// <summary>
    /// Decides which processes and drivers belong to the application when it is uninstalled.
    /// </summary>
    public static class ApplicationPathMatcher
    {
        // Data directory of MSI builds for every user profile.
        private const string UserDataSegment = @"\AppData\Local\CDPIUI\";

        /// <summary>
        /// Converts a process path or a service ImagePath to a plain Windows path
        /// </summary>
        public static string Normalize(string path)
        {
            string result = path.Trim().Trim('"').Replace('/', '\\');
            if (result.StartsWith(@"\??\", StringComparison.Ordinal))
                result = result.Substring(4);
            return result;
        }

        /// <summary>
        /// Checks whether a file is inside the installation directory or a user data directory of the application
        /// </summary>
        /// <param name="path">Process path or service ImagePath</param>
        /// <param name="installDirectory">Installation directory</param>
        public static bool BelongsToApplication(string? path, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(installDirectory))
                return false;

            string normalized = Normalize(path!);
            string root = Normalize(installDirectory).TrimEnd('\\') + "\\";

            return normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                normalized.IndexOf(UserDataSegment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
