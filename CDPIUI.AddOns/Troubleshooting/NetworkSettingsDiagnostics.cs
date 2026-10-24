using CDPIUI.Core.Basic;

namespace CDPIUI.AddOns.Troubleshooting;

/// <summary>
/// Reads and changes the Windows network settings the basic diagnostic checks: TCP timestamps
/// and encrypted DNS.
/// </summary>
internal static class NetworkSettingsDiagnostics
{
    public static async Task<bool> EnableTimestampsAsync()
    {
        try
        {
            var result = await SystemToolRunner.RunAsync(Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                "interface", "tcp", "set", "global", "timestamps=enabled");
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(TroubleshootingService), $"Cannot enable timestamps. {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> AreTimestampsEnabledAsync()
    {
        try
        {
            // netsh prints localized text and cannot pipe into findstr without a shell;
            // Get-NetTCPSetting returns the state as Enabled or Disabled in any language.
            var result = await SystemToolRunner.RunAsync(SystemToolRunner.PowerShellPath, "-NoProfile", "-NonInteractive", "-Command",
                "(Get-NetTCPSetting -SettingName Internet).Timestamps");
            return result.ExitCode == 0 &&
                string.Equals(result.Output.Trim(), "Enabled", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(TroubleshootingService), $"Cannot check timestamps. {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> IsEncryptedDnsConfiguredAsync()
    {
        try
        {
            var result = await SystemToolRunner.RunAsync(SystemToolRunner.PowerShellPath, "-NoProfile", "-NonInteractive", "-Command",
                "Get-ChildItem -Recurse -Path 'HKLM:System\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\' | Get-ItemProperty | Where-Object { $_.DohFlags -gt 0 } | Measure-Object | Select-Object -ExpandProperty Count");

            return int.TryParse(result.Output.Trim(), out int count) && count != 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(TroubleshootingService), $"Cannot check DNS. {ex.Message}");
            return false;
        }
    }
}
