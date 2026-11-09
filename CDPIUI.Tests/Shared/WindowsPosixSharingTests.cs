using System.Runtime.InteropServices;
using System.Text;
using CDPIUI.Shared.Security;
using Microsoft.Win32.SafeHandles;

namespace CDPIUI.Tests.Shared;

[Collection("Windows path leases")]
public sealed class WindowsPosixSharingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdpiui-posix-sharing-" + Guid.NewGuid().ToString("N"));
    public WindowsPosixSharingTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("read")]
    [InlineData("name")]
    [InlineData("anchor")]
    [InlineData("metadata_delete")]
    public void PosixReplacementCannotUnlinkAHeldName(string kind)
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_root, "target.bin");
        IDisposable held;
        using var parent = ReadOnlyPathLease.OpenDirectoryForMutation(_root);
        if (kind == "anchor")
        {
            held = ReadOnlyPathLease.OpenDirectoryForMutation(_root);
            target = Directory.EnumerateFiles(_root, ".cdpiui-directory-lease-*").First();
        }
        else
        {
            File.WriteAllText(target, "held bytes");
            held = kind == "read" ? ReadOnlyPathLease.OpenFile(target) : kind == "name" ? FileNameLease.OpenExisting(target)
                : WindowsFileHandles.OpenRelative(parent.DirectoryHandle, "target.bin", 0x10080, 3, 1, false, out _);
        }
        using (held)
        using (var source = WritablePathLease.CreateForMove(Path.Combine(_root, "source.bin")))
        {
            bool replaced = Replace(source.Stream.SafeFileHandle, target);
            int error = Marshal.GetLastWin32Error();
            Assert.False(replaced, "POSIX rename replaced the held " + kind + " name.");
            Assert.True(error is 32 or 5, "Unexpected Windows error " + error);
        }
    }

    [Fact]
    public void PosixReplacementControlWorksWhenTheTargetIsNotHeld()
    {
        if (!OperatingSystem.IsWindows()) return;
        string target = Path.Combine(_root, "target.bin");
        File.WriteAllText(target, "old");
        using var source = WritablePathLease.CreateForMove(Path.Combine(_root, "source.bin"));
        Assert.True(Replace(source.Stream.SafeFileHandle, target), "Windows error " + Marshal.GetLastWin32Error());
    }

    private static bool Replace(SafeFileHandle file, string target)
    {
        byte[] name = Encoding.Unicode.GetBytes(target);
        int rootOffset = IntPtr.Size == 8 ? 8 : 4, lengthOffset = rootOffset + IntPtr.Size, nameOffset = lengthOffset + 4;
        byte[] information = new byte[nameOffset + name.Length + 2];
        BitConverter.GetBytes(3u).CopyTo(information, 0); // REPLACE_IF_EXISTS | POSIX_SEMANTICS
        BitConverter.GetBytes((uint)name.Length).CopyTo(information, lengthOffset);
        name.CopyTo(information, nameOffset);
        return SetFileInformationByHandle(file, 22, information, (uint)information.Length); // FileRenameInfoEx
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, byte[] information, uint size);
}
