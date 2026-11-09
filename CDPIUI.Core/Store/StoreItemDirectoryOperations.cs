using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Security;

namespace CDPIUI.Core.Store;

/// <summary>Root-scoped Store mutations; database paths must match their item identifier.</summary>
internal static class StoreItemDirectoryOperations
{
    internal static void Delete(string itemsRoot, string itemId, string directory)
    {
        RequireItemPath(itemsRoot, itemId, directory);
        if (StoreItemIds.IsProtected(itemId)) throw new UnauthorizedAccessException("Protected Store folders cannot be deleted.");
        if (OperatingSystem.IsWindows()) WindowsFileRemoval.DeleteDirectoryIfExists(directory);
        else if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    internal static void Create(string itemsRoot, string itemId, string directory, bool clean)
    {
        RequireItemPath(itemsRoot, itemId, directory);
        if (clean && !StoreItemIds.IsProtected(itemId)) Delete(itemsRoot, itemId, directory);
        ArchivePaths.EnsureDirectoryExists(directory);
    }

    private static void RequireItemPath(string itemsRoot, string itemId, string directory)
    {
        if (!StoreItemIds.MatchesItemFolder(itemsRoot, itemId, directory))
            throw new UnauthorizedAccessException("The Store folder does not match its root and item identifier.");
    }
}
