using System.Diagnostics;
using CDPIUI.ElevatedLauncher;
using CDPIUI.Shared.Security;

namespace CDPIUI.Tests.Tools;

public class ElevatedLauncherTests
{
    [Theory]
    [InlineData("tray", "CDPIUI_TrayIcon.exe")]
    [InlineData("hosts", "EditHostFile.exe")]
    public void AllowedNameGivesItsFile(string name, string expected)
    {
        Assert.True(LaunchPolicy.TryGetTargetFileName(name, out string fileName));
        Assert.Equal(expected, fileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tray")]
    [InlineData("HOSTS")]
    [InlineData("tray ")]
    [InlineData("update")]
    [InlineData("CDPIUI_TrayIcon.exe")]
    [InlineData("EditHostFile.exe")]
    [InlineData(@"..\tray")]
    [InlineData(@"tray\..\Update.exe")]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    [InlineData(@"\\server\share\tool.exe")]
    public void OtherNameIsRejected(string name)
    {
        Assert.False(LaunchPolicy.TryGetTargetFileName(name, out string fileName));
        Assert.Equal(string.Empty, fileName);
    }

    [Fact]
    public void CallersUseTheNamesOfTheLauncher()
    {
        Assert.Equal(LaunchPolicy.TrayTarget, ElevatedLauncherCommand.TrayTarget);
        Assert.Equal(LaunchPolicy.HostsTarget, ElevatedLauncherCommand.HostsTarget);
        Assert.True(LaunchPolicy.TryGetTargetFileName(ElevatedLauncherCommand.TrayTarget, out string tray));
        Assert.Equal("CDPIUI_TrayIcon.exe", tray);
        Assert.True(LaunchPolicy.TryGetTargetFileName(ElevatedLauncherCommand.HostsTarget, out string hosts));
        Assert.Equal("EditHostFile.exe", hosts);
    }

    [Fact]
    public void OnlyTheHostsEditorIsAwaited()
    {
        Assert.True(LaunchPolicy.WaitsForExit(ElevatedLauncherCommand.HostsTarget));
        Assert.False(LaunchPolicy.WaitsForExit(ElevatedLauncherCommand.TrayTarget));
    }

    [Theory]
    [InlineData("DOTNET_ROOT")]
    [InlineData("dotnet_root")]
    [InlineData("DOTNET_ROOT(x86)")]
    [InlineData("DOTNET_ROOT_X64")]
    [InlineData("DOTNET_ROOT_ARM64")]
    [InlineData("DOTNET_STARTUP_HOOKS")]
    [InlineData("DOTNET_ADDITIONAL_DEPS")]
    [InlineData("DOTNET_SHARED_STORE")]
    [InlineData("DOTNET_DiagnosticPorts")]
    [InlineData("DOTNET_DbgMiniDumpName")]
    [InlineData("COREHOST_TRACE")]
    [InlineData("COREHOST_TRACEFILE")]
    [InlineData("CORECLR_ENABLE_PROFILING")]
    [InlineData("CORECLR_PROFILER")]
    [InlineData("CORECLR_PROFILER_PATH")]
    [InlineData("CORECLR_PROFILER_PATH_64")]
    [InlineData("COR_ENABLE_PROFILING")]
    [InlineData("COR_PROFILER_PATH")]
    [InlineData("COMPlus_EnableDiagnostics")]
    [InlineData("complus_gcServer")]
    public void RuntimeVariableIsFound(string name)
    {
        Assert.True(LaunchPolicy.IsRuntimeVariable(name));
    }

    [Theory]
    [InlineData("PATH")]
    [InlineData("TEMP")]
    [InlineData("SystemRoot")]
    [InlineData("COMSPEC")]
    [InlineData("DOTNET")]
    [InlineData("MY_DOTNET_ROOT")]
    [InlineData("CORE_LIBRARY")]
    [InlineData("COMPLUS")]
    [InlineData("CDPIUI_CATALOG")]
    public void OtherVariableIsKept(string name)
    {
        Assert.False(LaunchPolicy.IsRuntimeVariable(name));
    }

    [Fact]
    public void RuntimeVariablesAreRemovedAndOthersKept()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Path"] = @"C:\Windows\System32",
            ["dotnet_root_x64"] = @"C:\Users\user\runtime",
            ["CORECLR_ENABLE_PROFILING"] = "1",
            ["CORECLR_PROFILER_PATH"] = @"C:\Users\user\profiler.dll",
            ["COMPlus_EnableEventPipe"] = "1",
            ["COREHOST_TRACE"] = "1",
            ["TEMP"] = @"C:\Users\user\AppData\Local\Temp",
        };

        LaunchPolicy.RemoveRuntimeVariables(environment);

        Assert.Equal(new[] { "Path", "TEMP" }, environment.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void StartInfoEnvironmentLosesRuntimeVariables()
    {
        var startInfo = new ProcessStartInfo("target.exe");
        startInfo.Environment["DOTNET_STARTUP_HOOKS"] = "hook.dll";
        startInfo.Environment["DOTNET_ROOT"] = "runtime";
        startInfo.Environment["CDPIUI_LAUNCHER_TEST"] = "kept";

        LaunchPolicy.RemoveRuntimeVariables(startInfo.Environment);

        Assert.False(startInfo.Environment.ContainsKey("DOTNET_STARTUP_HOOKS"));
        Assert.False(startInfo.Environment.ContainsKey("DOTNET_ROOT"));
        Assert.Equal("kept", startInfo.Environment["CDPIUI_LAUNCHER_TEST"]);
    }

    [Theory]
    [InlineData("tray", "--autorun", "tray --autorun")]
    [InlineData("tray", "--show-ui --gui-args:YQ==", "tray --show-ui --gui-args:YQ==")]
    [InlineData("hosts", "/add", "hosts /add")]
    [InlineData("tray", "", "tray")]
    public void LauncherArgumentsStartWithTheTarget(string target, string arguments, string expected)
    {
        Assert.Equal(expected, ElevatedLauncherCommand.GetArguments(target, arguments));
    }

    [Fact]
    public void LauncherIsInTheApplicationFolder()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CDPI UI");

        Assert.Equal(Path.Combine(directory, "CDPIUI_ElevatedLauncher.exe"), ElevatedLauncherCommand.GetPath(directory));
    }
}
