using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Shared.Security;

/// <summary>Copies a held file's DACL for atomic creation, before private bytes are written.</summary>
internal static class FileAccessDescriptor
{
    public static byte[] Read(SafeFileHandle file)
    {
        const uint dacl = 4;
        GetKernelObjectSecurity(file, dacl, null, 0, out uint length);
        if (length == 0 || length > 1 << 20)
            throw new IOException("Cannot measure the file access descriptor.", Marshal.GetHRForLastWin32Error());
        byte[] descriptor = new byte[length];
        if (!GetKernelObjectSecurity(file, dacl, descriptor, length, out _))
            throw new IOException("Cannot read the file access descriptor.", Marshal.GetHRForLastWin32Error());
        return descriptor;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(SafeFileHandle handle, uint information,
        [Out] byte[]? descriptor, uint length, out uint requiredLength);
}
