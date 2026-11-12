#nullable enable

using System;

namespace CDPIUI.Helper.Basic;

internal enum AppActivationKind
{
    None,
    Launch,
    File,
    Protocol,
}

// WinRT activation is translated at the boundary; orchestration needs only these values.
internal sealed record AppActivationRequest(
    AppActivationKind Kind = AppActivationKind.None,
    string? Arguments = null,
    string[]? Files = null,
    string? ProtocolUri = null,
    Func<string?>? ReadLaunchArguments = null)
{
    public string? GetLaunchArguments() => ReadLaunchArguments != null ? ReadLaunchArguments() : Arguments;
}
