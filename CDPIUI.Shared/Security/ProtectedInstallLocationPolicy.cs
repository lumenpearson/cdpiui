using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CDPIUI.Shared.Security;

/// <summary>
/// Checks that an MSI installation and every directory leading to it cannot be changed by a
/// less privileged account. The registry path alone does not prove that its files are protected.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class ProtectedInstallLocationPolicy
{
    private const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    private const string CreatorOwnerSid = "S-1-3-0";

    private static readonly FileSystemRights MutationRights =
        FileSystemRights.WriteData |
        FileSystemRights.AppendData |
        FileSystemRights.CreateDirectories |
        FileSystemRights.WriteAttributes |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;

    /// <summary>
    /// Returns true when the installation and its ancestors are ordinary directories owned by
    /// trusted installers and no untrusted trustee has rights that can replace or modify them.
    /// </summary>
    public static bool IsProtected(string directory, string? trayUserSid)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(trayUserSid))
            return false;

        try
        {
            string fullDirectory = Path.GetFullPath(directory);
            string? current = fullDirectory;
            while (current != null)
            {
                if (!Directory.Exists(current) || !IsSafeDirectory(current, trayUserSid))
                    return false;

                var info = new DirectoryInfo(current);
                DirectoryInfo? parent = info.Parent;
                current = parent?.FullName;
            }

            // An attacker may have placed a user-owned executable or DLL in a directory before
            // MSI set the top-level ACL. Check the existing tree as well as its future write
            // permissions; the protected root prevents changes after this check.
            var pending = new Stack<string>();
            pending.Push(fullDirectory);
            while (pending.Count > 0)
            {
                string currentDirectory = pending.Pop();
                foreach (string entry in Directory.EnumerateFileSystemEntries(currentDirectory))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        return false;

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!IsSafeDirectory(entry, trayUserSid))
                            return false;
                        pending.Push(entry);
                    }
                    else if (!IsSafeFile(entry, trayUserSid))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSafeDirectory(string path, string trayUserSid)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            return false;

        DirectorySecurity security = new DirectoryInfo(path).GetAccessControl(
            AccessControlSections.Owner | AccessControlSections.Access);
        return HasProtectedOwnerAndAccess(security, trayUserSid);
    }

    private static bool IsSafeFile(string path, string trayUserSid)
    {
        FileSecurity security = new FileInfo(path).GetAccessControl(
            AccessControlSections.Owner | AccessControlSections.Access);
        return HasProtectedOwnerAndAccess(security, trayUserSid);
    }

    private static bool HasProtectedOwnerAndAccess(FileSystemSecurity security, string trayUserSid)
    {
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner ||
            !IsTrustedOwner(owner.Value, trayUserSid))
            return false;

        var descriptor = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);
        if (descriptor.DiscretionaryAcl == null)
            return false;

        foreach (FileSystemAccessRule rule in security.GetAccessRules(
            includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow ||
                (rule.FileSystemRights & MutationRights) == 0)
                continue;

            if (rule.IdentityReference is not SecurityIdentifier trustee ||
                !IsTrustedOwner(trustee.Value, trayUserSid))
                return false;
        }

        return true;
    }

    private static bool IsTrustedOwner(string sid, string trayUserSid) =>
        string.Equals(sid, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sid, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sid, TrustedInstallerSid, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sid, CreatorOwnerSid, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sid, trayUserSid, StringComparison.OrdinalIgnoreCase);
}
