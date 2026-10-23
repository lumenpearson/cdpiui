using CDPIUI.Core.Basic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CDPIUI.Core.Store
{
    public static partial class VersionHelper
    {
        /// <summary>
        /// Compare two version strings
        /// </summary>
        /// <param name="oldVersion">Version 1</param>
        /// <param name="newVersion">Version 2</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The
        /// return value has these meanings:
        /// <br></br>
        /// Value – Meaning<br></br>
        /// Less than zero – This instance precedes other in the sort order.<br></br>
        /// Zero – This instance occurs in the same position in the sort order as other.<br></br>
        /// Greater than zero – This instance follows other in the sort order.<br></br>
        ///</returns>
        public static int CompareVersionStrings(string? oldVersion, string? newVersion)
        {
            if (string.IsNullOrEmpty(oldVersion)  || string.IsNullOrEmpty(newVersion)) return 0;
            if (oldVersion == "%CURRENT%") return 0;

            if (oldVersion.StartsWith('v')) oldVersion = oldVersion[1..];
            if (newVersion.StartsWith('v')) newVersion = newVersion[1..];

            // "1.0.0rc1" -> "1.0.0-rc1"; "1.0.0-rc1" stays (it became "1.0.0--rc1").
            oldVersion = ReleaseCandidateRegex().Replace(oldVersion, "-rc");
            newVersion = ReleaseCandidateRegex().Replace(newVersion, "-rc");

            if (Semver.SemVersion.TryParse(oldVersion, out var oldSemVersion) && Semver.SemVersion.TryParse(newVersion, out var newSemVersion))
            {
                return Semver.SemVersion.ComparePrecedence(oldSemVersion, newSemVersion);
            }
            else if (Version.TryParse(oldVersion, out var oldVerVersion) && Version.TryParse(newVersion, out var newVerVersion))
            {
                return Normalize(oldVerVersion).CompareTo(Normalize(newVerVersion));
            }

            if (DateTime.TryParseExact(oldVersion, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime oldData) &&
                DateTime.TryParseExact(newVersion, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newData))
            {
                return DateTime.Compare(oldData, newData);
            }

            // Tags such as "release-70.6" or "zapret 70.6": compare the version numbers they contain.
            Match oldNumber = VersionNumberRegex().Match(oldVersion);
            Match newNumber = VersionNumberRegex().Match(newVersion);
            if (oldNumber.Success && newNumber.Success &&
                Version.TryParse(PadSingleNumber(oldNumber.Value), out var oldFallback) &&
                Version.TryParse(PadSingleNumber(newNumber.Value), out var newFallback))
            {
                return Normalize(oldFallback).CompareTo(Normalize(newFallback));
            }

            Logger.Instance.CreateErrorLog(nameof(VersionHelper), $"Cannot compare {oldVersion} and {newVersion}.");
            return 0;
        }

        // Version treats missing parts as -1, so 1.0 was less than 1.0.0.
        private static Version Normalize(Version version) =>
            new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

        private static string PadSingleNumber(string number) => number.Contains('.') ? number : number + ".0";

        [GeneratedRegex(@"(?<=\d)\.?rc", RegexOptions.IgnoreCase)]
        private static partial Regex ReleaseCandidateRegex();

        [GeneratedRegex(@"\d+(?:\.\d+){0,3}")]
        private static partial Regex VersionNumberRegex();

        public static bool IsVersionInRange(string? version, string? minVersion, string? maxVersion)
        {
            if (!Version.TryParse(version, out Version? current) || !Version.TryParse(minVersion, out Version? min))
                return false;

            if (current < min)
                return false;

            if (string.IsNullOrEmpty(maxVersion) || maxVersion == "NaN")
                return true;

            return Version.TryParse(maxVersion, out Version? max) && current <= max;
        }

        /// <summary>
        /// Is version string are correct
        /// </summary>
        /// <param name="version">Version string</param>
        /// <returns>true if correct, otherwise false</returns>
        public static bool IsVersionCorrect(string version)
        {
            return Version.TryParse(version, out var _);
        }

        /// <summary>
        /// Is id correct StoreId
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>true if correct, otherwise false</returns>
        public static bool IsIdCorrect(string id)
        {
            return CheckIdRegex().IsMatch(id);
        }

        [GeneratedRegex(@"^[a-zA-Z0-9\-]+$")]
        private static partial Regex CheckIdRegex();
    }
}
