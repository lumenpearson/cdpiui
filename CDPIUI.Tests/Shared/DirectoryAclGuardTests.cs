using System.ComponentModel;
using System.Security.Principal;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.TrayIcon.Helper.Basic;

namespace CDPIUI.Tests.Shared;

public sealed class DirectoryAclGuardTests
{
    [Fact]
    public void ExistingDirectoryCanBeUpdatedThroughItsHandle()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            string store = Directory.CreateDirectory(Path.Combine(root, "Store")).FullName;
            DirectoryAclGuard.GrantModify(store, CurrentUser());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SwappedParentCannotRedirectTheAccessChange()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        string link = Path.Combine(root, "Store");
        try
        {
            string target = Directory.CreateDirectory(Path.Combine(link, "Items")).FullName;
            string outside = Directory.CreateDirectory(Path.Combine(root, "outside", "Items")).FullName;
            Assert.True(TrustedPathValidator.IsInside(target, [link]));

            Directory.Move(link, Path.Combine(root, "original-store"));
            Directory.CreateSymbolicLink(link, Path.GetDirectoryName(outside)!);

            Assert.Throws<UnauthorizedAccessException>(() => DirectoryAclGuard.GrantModify(target, CurrentUser()));
        }
        finally
        {
            if (Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0)
                Directory.Delete(link);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LinkedTargetIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        string link = Path.Combine(root, "Store");
        try
        {
            string outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
            Directory.CreateSymbolicLink(link, outside);
            Assert.Throws<UnauthorizedAccessException>(() => DirectoryAclGuard.GrantModify(link, CurrentUser()));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MissingDirectoryIsNotCreatedByTheElevatedOperation()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = NewRoot();
        try
        {
            string missing = Path.Combine(root, "Store");
            Assert.Throws<Win32Exception>(() => DirectoryAclGuard.GrantModify(missing, CurrentUser()));
            Assert.False(Directory.Exists(missing));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewRoot() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cdpiui-acl-" + Guid.NewGuid().ToString("N"))).FullName;

    private static SecurityIdentifier CurrentUser() => WindowsIdentity.GetCurrent().User!;
}
