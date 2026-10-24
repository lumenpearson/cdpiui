using System.Runtime.Versioning;
using System.ServiceProcess;

namespace CDPIUI.AddOns.Troubleshooting;

/// <summary>
/// Looks up Windows services by name. The service list is read once and kept until
/// <see cref="Reset"/>, so one diagnostic run does not enumerate the services for every check.
/// Off Windows every lookup reports true.
/// </summary>
internal sealed class WindowsServiceInspector
{
    private ServiceController[]? _services;

    public void Reset() => _services = null;

    public bool Exists(string name)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return true;
            _services ??= ServiceController.GetServices();
            // A loop, not a lambda: the platform analyzer sees the Windows check above.
            foreach (var service in _services)
            {
                if (service.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool IsRunning(string name) =>
        IsRunning(service =>
            OperatingSystem.IsWindows() && service.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase));

    // Any matching service that runs: the first match alone could be a stopped one.
    public bool IsRunning(Func<ServiceController, bool> match)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return true;
            _services ??= ServiceController.GetServices();
            foreach (var service in _services)
            {
                if (match(service) &&
                    service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StopPending or ServiceControllerStatus.StartPending)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public static bool Matches(ServiceController service, string text) =>
        service.ServiceName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        service.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
}
