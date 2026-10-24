using System.Text.RegularExpressions;

namespace CDPIUI.Core.Store;

/// <summary>
/// Store item identifiers come from item metadata and local packs; they become folder names
/// that are created, replaced and deleted recursively
/// </summary>
public static class StoreItemIds
{
    private static readonly Regex ValidId = new(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

    public static bool IsValid(string? id) => id != null && ValidId.IsMatch(id);

    /// <summary>
    /// Checks that a directory is a single item folder inside the store items directory
    /// </summary>
    public static bool IsItemFolder(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        string root = Path.GetFullPath(CDPIUI.Core.Data.Directories.StoreItemsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && fullPath.Length > root.Length;
    }
}
