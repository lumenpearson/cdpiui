using System;
using System.IO;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CDPIUI.Shared.Security;

/// <summary>A package cache that remains protected when the tray exits or crashes.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class ProtectedStagingArea
{
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier System = new(WellKnownSidType.LocalSystemSid, null);
    private const long MaxPackageBytes = 1L << 30;

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "CDPIUI-Staging");

    public static string Stage(string root, string source)
    {
        string name = Path.GetFileName(source);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("A package must have an ordinary file name.", nameof(source));
        using ReadOnlyPathLease input = ReadOnlyPathLease.OpenFile(source);
        if (input.Stream!.Length > MaxPackageBytes)
            throw new InvalidDataException("The package exceeds the 1 GiB staging limit.");
        string parent = Path.GetDirectoryName(Path.GetFullPath(root))!;
        using ReadOnlyPathLease parentLease = ReadOnlyPathLease.OpenDirectory(parent);
        RequireProtectedPath(parent);
        CreatePrivateDirectory(root);
        using ReadOnlyPathLease rootLease = ReadOnlyPathLease.OpenDirectory(root);
        RequireProtectedPath(root);
        string session = Path.Combine(root, Guid.NewGuid().ToString("N"));
        CreatePrivateDirectory(session);
        using ReadOnlyPathLease sessionLease = ReadOnlyPathLease.OpenDirectory(session);
        RequireProtectedPath(session);
        string destination = Path.Combine(session, name);
        // CreateNew prevents following a planted leaf; the owner and DACL are supplied at
        // creation, not changed later while a medium-integrity owner could modify them.
        try
        {
            using (FileStream output = CreatePrivateFile(destination))
            {
                input.Stream.CopyTo(output);
                output.Flush(true);
            }
            return destination;
        }
        catch
        {
            sessionLease.Dispose();
            try { Release(root, destination); }
            catch (Exception error) { Trace.WriteLine($"Cannot remove failed staging session: {error.Message}"); }
            throw;
        }
    }

    public static void CreatePrivateDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(true, false);
        foreach (SecurityIdentifier sid in new[] { Administrators, System })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(security);
        // Create does not change an existing directory. Never repair or adopt an unsafe one.
        RequireProtectedPath(path);
    }

    public static FileStream CreatePrivateFile(string path)
    {
        var security = new FileSecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(true, false);
        foreach (SecurityIdentifier sid in new[] { Administrators, System })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.Read | FileSystemRights.Write,
            FileShare.None, 81920, FileOptions.None, security);
    }

    public static void RequireProtectedPath(string path)
    {
        bool ancestor = false;
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0 ||
                !HasProtectedAccess(new DirectoryInfo(current).GetAccessControl(), ancestor))
                throw new UnauthorizedAccessException($"The staging path is not protected: {current}");
            ancestor = true;
        }
    }

    internal static bool HasProtectedAccess(FileSystemSecurity security, bool ancestor)
    {
        const string trustedInstaller = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
        bool Trusted(string sid) => sid == Administrators.Value || sid == System.Value || sid == trustedInstaller;
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !Trusted(owner.Value) ||
            new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0).DiscretionaryAcl == null)
            return false;
        FileSystemRights mutation = FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes |
            FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        if (!ancestor) mutation |= FileSystemRights.WriteData | FileSystemRights.AppendData;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow ||
                (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0)
                continue;
            uint rights = unchecked((uint)rule.FileSystemRights);
            // Also recognize unmapped GENERIC_WRITE / GENERIC_ALL ACEs.
            if ((rights & ((uint)mutation | 0x50000000u)) != 0 &&
                (rule.IdentityReference is not SecurityIdentifier sid || !Trusted(sid.Value)))
                return false;
        }
        return true;
    }

    public static void RequireProtectedTree(string root)
    {
        RequireProtectedPath(root);
        foreach (string entry in Directory.EnumerateFileSystemEntries(root))
        {
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException($"A staging entry is a link: {entry}");
            if ((attributes & FileAttributes.Directory) != 0) RequireProtectedTree(entry);
            else if (!HasProtectedAccess(new FileInfo(entry).GetAccessControl(), ancestor: false))
                throw new UnauthorizedAccessException($"A staging file is not protected: {entry}");
        }
    }

    public static void Release(string root, string package)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        string session = Path.GetDirectoryName(Path.GetFullPath(package))!;
        if (!string.Equals(Path.GetDirectoryName(session), fullRoot, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(session), "N", out _))
            throw new UnauthorizedAccessException("The package is not in a staging session.");
        using ReadOnlyPathLease rootLease = ReadOnlyPathLease.OpenDirectory(root);
        RequireProtectedPath(root);
        RequireProtectedTree(session);
        Directory.Delete(session, true);
    }
}
