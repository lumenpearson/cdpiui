using System.Text.RegularExpressions;
using CDPIUI.Shared;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.Store;

/// <summary>
/// Store item identifiers come from item metadata and local packs; they become folder names
/// that are created, replaced and deleted recursively
/// </summary>
public static class StoreItemIds
{
    private static readonly Regex ValidId = new(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);

    public static bool IsValid(string? id) => id != null && ValidId.IsMatch(id) && WindowsPathNames.IsOrdinaryName(id) &&
        (!IsProtected(id) || id == SharedConstants.ApplicationStoreId || id == SharedConstants.LocalUserItemsId);

    public static bool IsProtected(string? id) =>
        string.Equals(id, SharedConstants.ApplicationStoreId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, SharedConstants.LocalUserItemsId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks that a directory is a single item folder inside the store items directory
    /// </summary>
    public static bool IsItemFolder(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;
        try
        {
            string fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string id = Path.GetFileName(fullPath);
            return !IsProtected(id) && MatchesItemFolder(CDPIUI.Core.Data.Directories.StoreItemsDirectory, id, fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    internal static bool MatchesItemFolder(string root, string itemId, string? directory)
    {
        if (!IsValid(itemId) || string.IsNullOrWhiteSpace(directory)) return false;
        try
        {
            string fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            WindowsPathNames.RequireOrdinaryAbsolutePath(fullPath);
            string expected = Path.GetFullPath(Path.Combine(root, itemId));
            WindowsPathNames.RequireOrdinaryAbsolutePath(expected);
            return string.Equals(fullPath, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}
