#nullable enable

using System.Linq;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace CDPIUI.Helper.Basic;

internal static class AppActivationRequestAdapter
{
    public static AppActivationRequest From(AppActivationArguments? activation, AppActivationKind? requestedKind = null)
    {
        if (requestedKind is null or AppActivationKind.Launch &&
            activation?.Kind == ExtendedActivationKind.Launch && activation.Data is ILaunchActivatedEventArgs launch)
        {
            return new(AppActivationKind.Launch, ReadLaunchArguments: () => launch.Arguments);
        }
        if (requestedKind is null or AppActivationKind.File &&
            activation?.Kind == ExtendedActivationKind.File && activation.Data is IFileActivatedEventArgs files)
        {
            return new(AppActivationKind.File, Files: files.Files.Select(file => file.Path).ToArray());
        }
        if (requestedKind is null or AppActivationKind.Protocol &&
            activation?.Kind == ExtendedActivationKind.Protocol && activation.Data is IProtocolActivatedEventArgs protocol)
        {
            return new(AppActivationKind.Protocol, ProtocolUri: protocol.Uri.ToString());
        }
        return new();
    }
}
