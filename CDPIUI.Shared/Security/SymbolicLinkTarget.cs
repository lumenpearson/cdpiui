using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Shared.Security;

/// <summary>Reads the target metadata of the opened symbolic file link, without following it.</summary>
internal static class SymbolicLinkTarget
{
    public static string Read(SafeFileHandle file, string linkPath)
    {
        byte[] data = new byte[16384];
        if (!DeviceIoControl(file, 0x900A8, IntPtr.Zero, 0, data, data.Length, out uint returned, IntPtr.Zero))
            throw new UnauthorizedAccessException("Cannot read the held symbolic-link metadata.");
        if (returned < 20 || returned > data.Length || BitConverter.ToUInt32(data, 0) != 0xA000000C)
            throw new UnauthorizedAccessException("An ordinary symbolic file link is required.");
        int dataLength = BitConverter.ToUInt16(data, 4);
        int offset = BitConverter.ToUInt16(data, 8), length = BitConverter.ToUInt16(data, 10);
        uint flags = BitConverter.ToUInt32(data, 16);
        if (dataLength < 12 || dataLength + 8 > returned || (offset & 1) != 0 || (length & 1) != 0 ||
            length == 0 || offset > dataLength - 12 - length || flags > 1)
            throw new UnauthorizedAccessException("Invalid symbolic-link metadata.");
        string target;
        try { target = new UnicodeEncoding(false, false, true).GetString(data, 20 + offset, length); }
        catch (DecoderFallbackException error) { throw new UnauthorizedAccessException("Invalid symbolic-link target encoding.", error); }
        if (flags == 0)
        {
            if (target.StartsWith(@"\??\UNC\", StringComparison.OrdinalIgnoreCase)) target = @"\\" + target.Substring(8);
            else if (target.StartsWith(@"\??\", StringComparison.Ordinal)) target = target.Substring(4);
            else throw new UnauthorizedAccessException("Unsupported absolute symbolic-link target.");
        }
        return Resolve(target, linkPath);
    }

    public static string Resolve(string target, string linkPath)
    {
        string full = Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(Path.GetDirectoryName(linkPath)!, target));
        WindowsPathNames.RequireOrdinaryAbsolutePath(full);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle file, uint code, IntPtr input, int inputLength,
        [Out] byte[] output, int outputLength, out uint returned, IntPtr overlapped);
}
