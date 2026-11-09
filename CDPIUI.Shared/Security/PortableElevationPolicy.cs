using System;

namespace CDPIUI.Shared.Security;

/// <summary>Allows an elevated portable copy only when its files belong to the same user.</summary>
public static class PortableElevationPolicy
{
    public static bool IsAllowed(
        bool installedByMsi,
        string? trayUserSid,
        string? desktopUserSid,
        string? applicationDirectoryOwnerSid)
    {
        if (installedByMsi)
            return true;

        return !string.IsNullOrWhiteSpace(trayUserSid) &&
            !string.IsNullOrWhiteSpace(applicationDirectoryOwnerSid) &&
            string.Equals(trayUserSid, applicationDirectoryOwnerSid, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(desktopUserSid) ||
                string.Equals(trayUserSid, desktopUserSid, StringComparison.OrdinalIgnoreCase));
    }
}
