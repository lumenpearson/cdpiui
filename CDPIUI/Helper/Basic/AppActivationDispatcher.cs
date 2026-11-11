#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic;

// Preserves the distinct first-start and redirected activation sequences. Actions own all effects.
internal sealed class AppActivationDispatcher
{
    private readonly Func<IEnumerable<string>, Task<bool>> _migrationArguments;
    private readonly Func<string?, Task<bool>> _migrationLaunch;
    private readonly Func<string[], Task<bool>> _files;
    private readonly Func<string, Task<bool>> _protocol;
    private readonly Func<string, Task<bool>> _coreDirect;
    private readonly Func<string, bool> _guiDirect;
    private readonly Func<string, string[]> _splitArguments;
    private readonly Action<Exception> _startupError;

    public AppActivationDispatcher(
        Func<IEnumerable<string>, Task<bool>> migrationArguments,
        Func<string?, Task<bool>> migrationLaunch,
        Func<string[], Task<bool>> files,
        Func<string, Task<bool>> protocol,
        Func<string, Task<bool>> coreDirect,
        Func<string, bool> guiDirect,
        Func<string, string[]> splitArguments,
        Action<Exception> startupError)
    {
        _migrationArguments = migrationArguments;
        _migrationLaunch = migrationLaunch;
        _files = files;
        _protocol = protocol;
        _coreDirect = coreDirect;
        _guiDirect = guiDirect;
        _splitArguments = splitArguments;
        _startupError = startupError;
    }

    public async Task<bool> ProcessStartupAsync(
        string[] arguments, Func<AppActivationKind, AppActivationRequest> getInitialActivation)
    {
        bool isFileProcessed = false;
        bool isActionPreferred = false;
        bool isMigrationProcessed = false;
        try
        {
            isMigrationProcessed = await _migrationArguments(arguments);
            if (!isMigrationProcessed && getInitialActivation(AppActivationKind.Launch) is { Kind: AppActivationKind.Launch } launch)
            {
                isMigrationProcessed = await _migrationLaunch(launch.GetLaunchArguments());
            }
            if (getInitialActivation(AppActivationKind.File) is { Kind: AppActivationKind.File } files)
            {
                isFileProcessed = await _files(files.Files!);
            }
            if (getInitialActivation(AppActivationKind.Protocol) is { Kind: AppActivationKind.Protocol } protocol)
            {
                isActionPreferred = await _protocol(protocol.ProtocolUri!);
            }
            if (!isFileProcessed)
            {
                isFileProcessed = await _files(arguments);
            }

            string? protocolArg = isActionPreferred
                ? null
                : arguments.FirstOrDefault(value => value.StartsWith("----ms-protocol:"));
            if (protocolArg != null)
            {
                isActionPreferred = await _protocol(protocolArg["----ms-protocol:".Length..]);
            }
            string? directArgs = arguments.FirstOrDefault(value => value.StartsWith("--direct:"));
            if (directArgs != null)
            {
                string value = directArgs["--direct:".Length..];
                isActionPreferred = await _coreDirect(value);
                if (!isActionPreferred)
                {
                    isActionPreferred = _guiDirect(value);
                }
            }
        }
        catch (Exception exception)
        {
            _startupError(exception);
        }
        return isFileProcessed || isActionPreferred || isMigrationProcessed;
    }

    // Exceptions intentionally reach App's existing redirected-activation error boundary.
    public async Task<bool> ProcessRedirectedAsync(AppActivationRequest activation)
    {
        bool result = false;
        if (activation.Kind == AppActivationKind.Protocol)
        {
            result = await _protocol(activation.ProtocolUri!);
        }
        if (activation.Kind == AppActivationKind.File)
        {
            result = await _files(activation.Files!);
        }
        else if (activation.Kind == AppActivationKind.Launch)
        {
            result = await _migrationLaunch(activation.GetLaunchArguments());
            string[] args = _splitArguments(activation.GetLaunchArguments()!);
            if (!result)
            {
                string? directArgs = args.FirstOrDefault(value => value.StartsWith("--direct:"));
                if (directArgs != null)
                {
                    string value = directArgs["--direct:".Length..];
                    result = await _coreDirect(value) || _guiDirect(value);
                }
            }
            if (!result)
            {
                result = await _files(args);
            }
        }
        return result;
    }
}
