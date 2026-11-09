using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

#nullable enable
#if UPDATE_TOOL
namespace Update;
#else
namespace CDPIUI.Shared.Security;
#endif

/// <summary>Single-component Windows operations relative to an already opened directory.</summary>
internal static class WindowsFileHandles
{
    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr Root, Name;
        public uint Attributes;
        public IntPtr SecurityDescriptor, SecurityQualityOfService;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatus { public IntPtr Status; public UIntPtr Information; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Information
    {
        public uint Attributes;
        public uint CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    public static void RequireOrdinaryDirectory(SafeFileHandle directory)
    {
        if (!GetFileInformationByHandle(directory, out Information info))
            throw new IOException("Cannot inspect the parent directory.", Marshal.GetHRForLastWin32Error());
        if ((info.Attributes & 0x410) != 0x10)
            throw new UnauthorizedAccessException("The parent is no longer an ordinary directory.");
    }

    // NtCreateFile dispositions: OPEN=1, CREATE=2, OPEN_IF=3. No mode truncates bytes.
    public static SafeFileHandle OpenRelative(SafeFileHandle directory, string name, uint access,
        uint share, uint disposition, bool isDirectory, out bool created,
        bool deleteOnClose = false, byte[]? securityDescriptor = null, bool allowLeafReparse = false)
    {
        if (!WindowsPathNames.IsOrdinaryName(name)) throw new ArgumentException("A single ordinary name is required.", nameof(name));
        bool retained = false;
        IntPtr text = IntPtr.Zero, unicode = IntPtr.Zero;
        GCHandle pinned = default;
        try
        {
            directory.DangerousAddRef(ref retained);
            text = Marshal.StringToHGlobalUni(name);
            var value = new UnicodeString { Length = checked((ushort)(name.Length * 2)), MaximumLength = checked((ushort)(name.Length * 2 + 2)), Buffer = text };
            unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(value, unicode, false);
            if (securityDescriptor != null) pinned = GCHandle.Alloc(securityDescriptor, GCHandleType.Pinned);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(), Root = directory.DangerousGetHandle(), Name = unicode,
                // Intentional file-name operations may inspect the reparse leaf itself.
                // The parent is still held, ordinary, and the name has one component.
                Attributes = allowLeafReparse ? 0x40u : 0x1040u, // OBJ_CASE_INSENSITIVE | OBJ_DONT_REPARSE
                SecurityDescriptor = pinned.IsAllocated ? pinned.AddrOfPinnedObject() : IntPtr.Zero,
            };
            uint options = 0x00200000u | 0x20u | (isDirectory ? 1u : 0x40u) | (deleteOnClose ? 0x1000u : 0u);
            int status = NtCreateFile(out SafeFileHandle handle, access | 0x00100000u, ref attributes,
                out IoStatus io, IntPtr.Zero, 0x80, share, disposition, options, IntPtr.Zero, 0);
            created = io.Information.ToUInt64() == 2; // FILE_CREATED
            if (status != 0)
            {
                handle?.Dispose();
                ThrowStatus(status, name, isDirectory);
            }
            return handle!;
        }
        finally
        {
            if (pinned.IsAllocated) pinned.Free();
            if (unicode != IntPtr.Zero) Marshal.FreeHGlobal(unicode);
            if (text != IntPtr.Zero) Marshal.FreeHGlobal(text);
            if (retained) directory.DangerousRelease();
        }
    }

    public readonly struct Entry
    {
        public string Name { get; }
        public FileAttributes Attributes { get; }
        public Entry(string name, FileAttributes attributes) { Name = name; Attributes = attributes; }
    }

    public static void Rename(SafeFileHandle file, SafeFileHandle directory, string name, bool replace)
    {
        if (!WindowsPathNames.IsOrdinaryName(name)) throw new ArgumentException("A single ordinary name is required.", nameof(name));
        bool retained = false;
        try
        {
            directory.DangerousAddRef(ref retained);
            RequireOrdinaryDirectory(directory);
            byte[] text = Encoding.Unicode.GetBytes(name);
            int rootOffset = IntPtr.Size == 8 ? 8 : 4;
            int lengthOffset = rootOffset + IntPtr.Size;
            int nameOffset = lengthOffset + 4;
            byte[] information = new byte[nameOffset + text.Length + 2];
            Array.Copy(BitConverter.GetBytes(replace ? 1u : 0u), 0, information, 0, 4);
            byte[] root = IntPtr.Size == 8 ? BitConverter.GetBytes(directory.DangerousGetHandle().ToInt64())
                : BitConverter.GetBytes(directory.DangerousGetHandle().ToInt32());
            Array.Copy(root, 0, information, rootOffset, root.Length);
            Array.Copy(BitConverter.GetBytes((uint)text.Length), 0, information, lengthOffset, 4);
            Array.Copy(text, 0, information, nameOffset, text.Length);
            int status = NtSetInformationFile(file, out _, information, (uint)information.Length, 10); // FileRenameInformation
            if (status != 0) ThrowStatus(status, "the held rename target", false);
        }
        finally { if (retained) directory.DangerousRelease(); }
    }

    public static IEnumerable<Entry> Enumerate(SafeFileHandle directory)
    {
        const int capacity = 65536;
        IntPtr buffer = Marshal.AllocHGlobal(capacity);
        bool retained = false;
        try
        {
            directory.DangerousAddRef(ref retained);
            bool restart = true;
            while (true)
            {
                RequireOrdinaryDirectory(directory);
                int status = NtQueryDirectoryFile(directory, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out IoStatus io,
                    buffer, capacity, 1, false, IntPtr.Zero, restart); // FileDirectoryInformation
                restart = false;
                RequireOrdinaryDirectory(directory);
                if (status == unchecked((int)0x80000006)) yield break; // STATUS_NO_MORE_FILES
                if (status != 0) ThrowStatus(status, "directory enumeration", true);
                int length = checked((int)io.Information.ToUInt64());
                int offset = 0;
                while (true)
                {
                    if (length > capacity || offset > length - 64) throw new IOException("Invalid directory information.");
                    IntPtr entry = IntPtr.Add(buffer, offset);
                    int next = Marshal.ReadInt32(entry);
                    int nameBytes = Marshal.ReadInt32(entry, 60);
                    if (nameBytes < 0 || (nameBytes & 1) != 0 || nameBytes > length - offset - 64)
                        throw new IOException("Invalid directory name length.");
                    string name = Marshal.PtrToStringUni(IntPtr.Add(entry, 64), nameBytes / 2)!;
                    if (name != "." && name != "..")
                    {
                        if (!WindowsPathNames.IsOrdinaryName(name)) throw new UnauthorizedAccessException("An ambiguous directory entry was found.");
                        yield return new Entry(name, (FileAttributes)Marshal.ReadInt32(entry, 56));
                    }
                    if (next == 0) break;
                    if (next < 64 + nameBytes || next > length - offset) throw new IOException("Invalid directory entry offset.");
                    offset += next;
                }
            }
        }
        finally
        {
            if (retained) directory.DangerousRelease();
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ThrowStatus(int status, string path, bool directory)
    {
        uint code = RtlNtStatusToDosError(status);
        if (code == 2 && !directory) throw new FileNotFoundException($"Cannot open {path}.", path);
        if (code == 3 || (code == 2 && directory)) throw new DirectoryNotFoundException($"Cannot open {path}.");
        if (code == 4395) throw new UnauthorizedAccessException("A reparse point was encountered.");
        throw new IOException($"Cannot access {path} (Windows error {code}).", unchecked((int)(0x80070000u | code)));
    }

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref ObjectAttributes attributes,
        out IoStatus io, IntPtr allocation, uint fileAttributes, uint share, uint disposition, uint options, IntPtr ea, uint eaLength);
    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(SafeFileHandle file, out IoStatus io, byte[] information, uint length, int informationClass);
    [DllImport("ntdll.dll")]
    private static extern int NtQueryDirectoryFile(SafeFileHandle directory, IntPtr @event, IntPtr apc, IntPtr context,
        out IoStatus io, IntPtr buffer, uint length, int informationClass,
        [MarshalAs(UnmanagedType.U1)] bool single, IntPtr pattern, [MarshalAs(UnmanagedType.U1)] bool restart);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information information);
}
