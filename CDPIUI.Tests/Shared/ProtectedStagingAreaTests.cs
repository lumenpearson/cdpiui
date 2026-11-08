using System.Security.AccessControl;
using System.Security.Principal;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class ProtectedStagingAreaTests
{
    [Fact]
    public void NullDaclIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var admin = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var raw = new RawSecurityDescriptor(ControlFlags.DiscretionaryAclPresent, admin, admin, null, null);
        byte[] bytes = new byte[raw.BinaryLength];
        raw.GetBinaryForm(bytes, 0);
        var security = new DirectorySecurity();
        security.SetSecurityDescriptorBinaryForm(bytes);
        Assert.False(ProtectedStagingArea.HasProtectedAccess(security, ancestor: false));
    }

    [Theory]
    [InlineData("not-a-session")]
    [InlineData("")]
    public void ReleaseRejectsPathsWithoutAnOwnedSession(string session)
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = Path.Combine(Path.GetTempPath(), "not-created-staging-root");
        string package = Path.Combine(root, session, "package.bin");
        Assert.Throws<UnauthorizedAccessException>(() => ProtectedStagingArea.Release(root, package));
    }

    [Fact]
    public void AlternateStreamPackageNameIsRejectedBeforeCopying()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<ArgumentException>(() => ProtectedStagingArea.Stage("not-created", "package.msi:stream"));
    }

    [Theory]
    [InlineData(FileSystemRights.CreateDirectories, true, true)]
    [InlineData(FileSystemRights.CreateDirectories, false, false)]
    [InlineData(FileSystemRights.DeleteSubdirectoriesAndFiles, true, false)]
    [InlineData(FileSystemRights.WriteAttributes, true, false)]
    [InlineData(FileSystemRights.ReadAndExecute, false, true)]
    [InlineData(FileSystemRights.FullControl, true, false)]
    public void ChildCreationOnAncestorDoesNotAllowReplacingProtectedChild(FileSystemRights rights, bool ancestor, bool expected)
    {
        if (!OperatingSystem.IsWindows()) return;
        var security = new DirectorySecurity();
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            rights, AccessControlType.Allow));
        Assert.Equal(expected, ProtectedStagingArea.HasProtectedAccess(security, ancestor));
    }

    [Fact]
    public void UserOwnerIsRejectedEvenWithAnOtherwiseEmptyDacl()
    {
        if (!OperatingSystem.IsWindows()) return;
        var security = new DirectorySecurity();
        security.SetOwner(WindowsIdentity.GetCurrent().User!);
        security.SetAccessRuleProtection(true, false);
        Assert.False(ProtectedStagingArea.HasProtectedAccess(security, ancestor: false));
    }

    [Fact]
    public void UserOwnedPathIsNotAdoptedOrRepaired()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = Path.Combine(Path.GetTempPath(), "cdpiui-unsafe-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string before = new DirectoryInfo(root).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);
            Assert.Throws<UnauthorizedAccessException>(() => ProtectedStagingArea.CreatePrivateDirectory(root));
            Assert.Equal(before, new DirectoryInfo(root).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All));
        }
        finally { Directory.Delete(root); }
    }
}
