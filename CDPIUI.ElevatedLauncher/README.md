**English** | [Русский](README.ru.md)

# CDPIUI.ElevatedLauncher

`CDPIUI_ElevatedLauncher.exe` starts the elevated programs of CDPI UI: the tray (`CDPIUI_TrayIcon.exe`)
and the hosts file editor (`EditHostFile.exe`). It is a Native AOT program for `win-x64`. Its
manifest requires administrator rights (`app.manifest`). Thus the UAC prompt shows the launcher, and
the program that it starts gets its token.

## Why the launcher exists

UAC makes the environment of an elevated process from the user profile. Thus the user variables
(`HKCU\Environment`) apply to the process, and each process of the user can change these variables.
The tray and `EditHostFile.exe` are framework-dependent .NET programs. The .NET host and CoreCLR read
some variables. For example, `DOTNET_ROOT` selects the runtime, `CORECLR_PROFILER_PATH` loads a
profiler DLL, and `DOTNET_STARTUP_HOOKS` loads an assembly. A started elevated program would then run
code of the user with administrator rights.

The launcher has no .NET host and no CLR. Thus it does not read the runtime, profiler and startup hook
variables. It removes them before it starts the program.

## Arguments

```
CDPIUI_ElevatedLauncher.exe <target> [arguments of the target]
```

| Target | Program | The launcher waits for the exit |
|---|---|---|
| `tray` | `CDPIUI_TrayIcon.exe` | No. It exits with `0` after the start. |
| `hosts` | `EditHostFile.exe` | Yes. It exits with the exit code of `EditHostFile.exe`. |

- The target name must be equal to a name in the table. The comparison is case-sensitive.
- The program is in the folder of the launcher. The launcher never takes a path from the arguments.
- The launcher gives the other arguments to the program unchanged. It adds each argument to `ProcessStartInfo.ArgumentList`, so the program gets the same arguments.
- The working directory of the program is the folder of the launcher.
- The launcher starts the program with `UseShellExecute = false`. The program gets the token of the launcher and shows no second UAC prompt.

## Environment

The launcher copies its environment to the program. It removes each variable whose name starts with
one of these prefixes, without case sensitivity:

- `DOTNET_`
- `COREHOST_`
- `CORECLR_`
- `COR_`
- `COMPlus_`

The prefixes include `DOTNET_ROOT`, `DOTNET_ROOT(x86)` and `DOTNET_ROOT_X64`. `LaunchPolicy` also
names these three variables, so a change of the prefixes cannot keep them.

The launcher keeps all other variables, for example `PATH` and `TEMP`.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | The tray started. |
| Exit code of `EditHostFile.exe` | Target `hosts`. Refer to [EditHostFile/README.md](../EditHostFile/README.md#exit-codes). |
| `0x80070057` (`E_INVALIDARG`) | No target, or a target that is not in the table. |
| `0x80070002` | The program is not in the folder of the launcher. |
| `0x8007xxxx` | Windows did not start the program. The low 16 bits are the Win32 error. |
| `0x80004005` (`E_FAIL`) | Another error. |

The GUI converts the codes of the `hosts` target to errors with the `HOSTEDIT` prefix.

## Callers

| Caller | Command |
|---|---|
| GUI, when it cannot connect to the tray (`CDPIUI.Core/Communication/PipeClientService.cs`) | `tray --autorun` or `tray --show-ui [...]`, verb `runas` |
| Autorun task `CDPIUI_Autostart` (`CDPIUI.TrayIcon/Helper/Settings/AutoStartManager.cs`) | `tray --autorun` |
| Hosts file editor (`CDPIUI.AddOns/HostsFileEditor/HostsFileEditorService.cs`) | `hosts /add`, `hosts /remove` or `hosts /recover`, verb `runas` |

The callers get the file name and the target names from `ElevatedLauncherCommand`
(`CDPIUI.Shared/Security/ElevatedLauncherCommand.cs`).

The processes that the tray starts (components, GoodCheck, `Update.exe`) get the environment of the
tray. The launcher removed the runtime variables from this environment. Refer to
[CDPIUI.TrayIcon/README.md](../CDPIUI.TrayIcon/README.md#environment-of-an-elevated-start).

## Files

| File | Content |
|---|---|
| `Program.cs` | The entry point: finds the target, removes the variables, starts the program |
| `LaunchPolicy.cs` | The target table and the variable filter |
| `app.manifest` | `requireAdministrator` |

## Build

`CDPIUI.ElevatedLauncher.csproj` sets `OutputType` `WinExe` (no console window), `net8.0`, `win-x64`,
`PublishAot`, `InvariantGlobalization` and `StartupHookSupport` `false`.

To check the code, build the project. The build runs the trim and AOT analyzers:

```
dotnet build CDPIUI.ElevatedLauncher/CDPIUI.ElevatedLauncher.csproj -c Release -p:Platform=x64
```

To make the native file, publish it on Windows. Native AOT needs the C++ build tools of Visual Studio
(workload "Desktop development with C++"):

```
dotnet publish CDPIUI.ElevatedLauncher/CDPIUI.ElevatedLauncher.csproj -c Release -p:Platform=x64 -r win-x64 -o artifacts/tools/launcher
```

`.github/scripts/build_portable.ps1` uses the same command, checks that the file is not a .NET
assembly and copies it into the release layout. The `tools` job of CI publishes it with
`TreatWarningsAsErrors`, so a trim or AOT warning fails the job.

On Linux or macOS, the native link step is not available. To see the AOT warnings, add
`-p:EnableWindowsTargeting=true -p:DisableUnsupportedError=true`. The compiler then reports the
warnings, and the link step fails.

## Tests

`CDPIUI.Tests` compiles `LaunchPolicy.cs` as a linked file. It does not reference the Native AOT
project. `CDPIUI.Tests/Tools/ElevatedLauncherTests.cs` tests the target table, the variable filter
and the arguments of `ElevatedLauncherCommand`.

## Rules

- Keep the launcher small. Do not add NuGet packages, reflection, JSON or other code that Native AOT cannot compile without warnings.
- Do not add a target that takes a path or a file name from the arguments. Each target is a fixed file name in the folder of the launcher.
- Update the target names in `LaunchPolicy` and `ElevatedLauncherCommand` together. A test compares them.

CAUTION: Do not remove `requireAdministrator` from `app.manifest`, and do not publish the launcher
without Native AOT. A launcher with a .NET host reads the variables that it must remove.
