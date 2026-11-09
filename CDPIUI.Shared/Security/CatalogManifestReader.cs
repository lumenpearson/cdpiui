using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Security;

/// <summary>Reads catalog metadata without asking PowerShell to traverse the source tree.</summary>
internal static class CatalogManifestReader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    internal const int MaxOutputCharacters = 16 * 1024 * 1024;

    public static async Task<CatalogManifest?> ReadAsync(string catalogFile)
    {
        string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(powershell)
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        // Pass only the catalog itself and explicitly skip its name. Windows PowerShell
        // otherwise tries to hash it as a package member. No directory is traversed.
        start.ArgumentList.Add(
            "$ErrorActionPreference='Stop'; " +
            "Import-Module ($PSHOME + '\\Modules\\Microsoft.PowerShell.Security\\Microsoft.PowerShell.Security.psd1'); " +
            "$c = Microsoft.PowerShell.Security\\Test-FileCatalog -CatalogFilePath $env:CDPIUI_CATALOG " +
            "-Path ([System.Management.Automation.WildcardPattern]::Escape($env:CDPIUI_CATALOG)) " +
            "-FilesToSkip ([System.Management.Automation.WildcardPattern]::Escape([IO.Path]::GetFileName($env:CDPIUI_CATALOG))) -Detailed; " +
            "'CDPIUI-CATALOG-V1'; $c.HashAlgorithm; " +
            "foreach($e in $c.CatalogItems.GetEnumerator()) { " +
            "[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($e.Key)) + ':' + $e.Value }");
        start.Environment["CDPIUI_CATALOG"] = Path.GetFullPath(catalogFile);
        using var process = Process.Start(start);
        if (process == null) return null;
        Task<string?> output = ReadBoundedAsync(process.StandardOutput, MaxOutputCharacters, process);
        Task<string?> errors = ReadBoundedAsync(process.StandardError, 65536, process);
        Task exited = Task.Run(() => process.WaitForExit());
        if (await Task.WhenAny(exited, Task.Delay(Timeout)) != exited)
        {
            TryKill(process);
            return null;
        }
        await Task.WhenAll(output, errors);
        if (process.ExitCode != 0 || output.Result == null || errors.Result == null) return null;
        return CatalogManifest.Parse(output.Result);
    }

    private static async Task<string?> ReadBoundedAsync(StreamReader reader, int maximum, Process process)
    {
        char[] buffer = new char[4096];
        var output = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            if (output.Length > maximum - count) { TryKill(process); return null; }
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(); }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException) { }
    }
}
