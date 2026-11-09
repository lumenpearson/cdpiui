using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Shared;

public sealed class PortableElevationPolicyTests
{
    [Fact]
    public void MsiInstallation_AllowsDifferentUsersAndDirectoryOwner()
    {
        Assert.True(PortableElevationPolicy.IsAllowed(
            true, "S-1-5-21-admin", "S-1-5-21-user", "S-1-5-21-owner"));
    }

    [Fact]
    public void PortableCopy_AllowsSameTrayDesktopUserAndDirectoryOwner()
    {
        Assert.True(PortableElevationPolicy.IsAllowed(
            false, "S-1-5-21-user", "s-1-5-21-user", "S-1-5-21-user"));
    }

    [Fact]
    public void PortableCopy_AllowsOwnedDirectoryBeforeDesktopShellStarts()
    {
        Assert.True(PortableElevationPolicy.IsAllowed(
            false, "S-1-5-21-user", null, "S-1-5-21-user"));
    }

    [Theory]
    [InlineData("S-1-5-21-admin", "S-1-5-21-user", "S-1-5-21-user")]
    [InlineData("S-1-5-21-admin", "S-1-5-21-admin", "S-1-5-21-user")]
    [InlineData("S-1-5-21-user", "S-1-5-21-user", "S-1-5-21-admin")]
    public void PortableCopy_RejectsDifferentDesktopUserOrDirectoryOwner(
        string trayUserSid,
        string desktopUserSid,
        string applicationDirectoryOwnerSid)
    {
        Assert.False(PortableElevationPolicy.IsAllowed(
            false, trayUserSid, desktopUserSid, applicationDirectoryOwnerSid));
    }

    [Theory]
    [InlineData(null, "S-1-5-21-user", "S-1-5-21-user")]
    [InlineData("S-1-5-21-user", "S-1-5-21-user", null)]
    [InlineData("", "S-1-5-21-user", "S-1-5-21-user")]
    [InlineData("S-1-5-21-user", "S-1-5-21-user", "")]
    public void PortableCopy_RejectsWhenIdentityCannotBeVerified(
        string? trayUserSid,
        string? desktopUserSid,
        string? applicationDirectoryOwnerSid)
    {
        Assert.False(PortableElevationPolicy.IsAllowed(
            false, trayUserSid, desktopUserSid, applicationDirectoryOwnerSid));
    }
}
