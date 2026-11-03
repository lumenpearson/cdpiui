using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.TrayIcon.Helper.Basic;

/// <summary>
/// Grants access to the directory that was opened, rather than resolving its name again
/// after the path check. The caller checks that the name is in an allowed Store root.
/// </summary>
internal static class DirectoryAclGuard
{
    private const uint ReadControl = 0x00020000;
    private const uint WriteDac = 0x00040000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint DaclSecurityInformation = 0x00000004;
    private const uint SeFileObject = 1;
    private const uint GrantAccessMode = 1;
    private const uint TrusteeIsSid = 0;
    private const uint TrusteeIsUser = 1;

    public static void GrantModify(string path, SecurityIdentifier user)
    {
        string requestedPath = Path.GetFullPath(path);
        // Do not create a missing directory here. Its parent could be changed to a junction
        // after the caller's path check, and creation would then happen outside the Store.
        using SafeFileHandle directory = CreateFileW(requestedPath,
            ReadControl | WriteDac | FileReadAttributes, FileShare.ReadWrite,
            IntPtr.Zero, FileMode.Open,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
        if (directory.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (!GetFileInformationByHandle(directory, out FileInformation info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        FileAttributes attributes = (FileAttributes)info.Attributes;
        if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("The access target is not an ordinary directory.");

        var pathBuffer = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandleW(directory, pathBuffer, (uint)pathBuffer.Capacity, 0);
        if (length == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (length >= pathBuffer.Capacity)
            throw new PathTooLongException("The resolved access target is too long.");
        string actualPath = FromWin32FinalPath(pathBuffer.ToString());
        if (!string.Equals(Path.GetFullPath(actualPath), requestedPath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The access target resolves to another directory.");

        uint error = GetSecurityInfo(directory, SeFileObject, DaclSecurityInformation,
            IntPtr.Zero, IntPtr.Zero, out IntPtr oldDacl, IntPtr.Zero, out IntPtr securityDescriptor);
        if (error != 0)
            throw new Win32Exception((int)error);

        IntPtr sid = IntPtr.Zero;
        IntPtr newDacl = IntPtr.Zero;
        try
        {
            // A null DACL already allows everyone; never replace it with a new ACL.
            if (oldDacl == IntPtr.Zero)
                return;

            byte[] sidBytes = new byte[user.BinaryLength];
            user.GetBinaryForm(sidBytes, 0);
            sid = Marshal.AllocHGlobal(sidBytes.Length);
            Marshal.Copy(sidBytes, 0, sid, sidBytes.Length);

            var entry = new ExplicitAccess
            {
                AccessPermissions = (uint)FileSystemRights.Modify,
                AccessMode = GrantAccessMode,
                Inheritance = (uint)(InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit),
                Trustee = new Trustee { Form = TrusteeIsSid, Type = TrusteeIsUser, Name = sid }
            };
            error = SetEntriesInAclW(1, ref entry, oldDacl, out newDacl);
            if (error != 0)
                throw new Win32Exception((int)error);

            error = SetSecurityInfo(directory, SeFileObject, DaclSecurityInformation,
                IntPtr.Zero, IntPtr.Zero, newDacl, IntPtr.Zero);
            if (error != 0)
                throw new Win32Exception((int)error);
        }
        finally
        {
            if (newDacl != IntPtr.Zero)
                LocalFree(newDacl);
            if (sid != IntPtr.Zero)
                Marshal.FreeHGlobal(sid);
            if (securityDescriptor != IntPtr.Zero)
                LocalFree(securityDescriptor);
        }
    }

    private static string FromWin32FinalPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return @"\\" + path.Substring(8);
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            return path.Substring(4);
        return path;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Trustee
    {
        public IntPtr MultipleTrustee;
        public uint MultipleTrusteeOperation;
        public uint Form;
        public uint Type;
        public IntPtr Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExplicitAccess
    {
        public uint AccessPermissions;
        public uint AccessMode;
        public uint Inheritance;
        public Trustee Trustee;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint desiredAccess, FileShare shareMode,
        IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path,
        uint pathLength, uint flags);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInformation,
        IntPtr owner, IntPtr group, out IntPtr dacl, IntPtr sacl, out IntPtr securityDescriptor);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SetEntriesInAclW(uint count, ref ExplicitAccess entries, IntPtr oldAcl, out IntPtr newAcl);

    [DllImport("advapi32.dll")]
    private static extern uint SetSecurityInfo(SafeFileHandle handle, uint objectType, uint securityInformation,
        IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
