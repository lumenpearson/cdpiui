# Contributing to CDPI UI

This page explains how the application is put together, how to build it and what to keep in mind when changing it.
Each project also has its own README with details; the list is at the end of this page.

## How the application works

CDPI UI consists of two processes that talk to each other over a named pipe:

| Part | Projects | Runs as | Role |
|---|---|---|---|
| GUI | `CDPIUI`, `CDPIUI.AddOns`, `CDPIUI.Core`, `CDPIUI.Shared` | the signed-in user (not elevated) | windows, Store, presets, add-ons, settings |
| Tray (background worker) | `CDPIUI.TrayIcon`, `CDPIUI.Shared` | administrator | starts and stops components, system proxy, autorun, notifications, ConditionalLaunch, MSI installation |

Two small .NET Framework 4.7.2 tools complete the application: `Update` (applies application patches) and `EditHostFile` (edits the hosts file with administrator rights). `NewSetup` builds the MSI installer.

The GUI never starts components itself: it sends a request to the tray, and the tray reports the process state and output back. If the GUI starts while no tray is running, it starts the tray and exits; the tray then starts the GUI again with the original arguments.

## Build

Requirements: Windows 10/11 x64, Visual Studio 2022 with the .NET desktop and WinUI workloads (or the .NET 8 SDK plus MSBuild from Visual Studio for the GUI), PowerShell 7.

1. Create the secrets class. `CDPIUI.Shared/Secrets/Secret.cs` must contain a `public static class Secret` in the `CDPIUI.Shared.Secrets` namespace with the members described by the `ISecret` interface in [SecretExample.cs](../CDPIUI.Shared/Secrets/SecretExample.cs) (copy only the members; the interface itself already exists there).
   For a local build without Store access run `pwsh .github/scripts/create_secret_stub.ps1`: it creates the class with empty values. The file is ignored by git.
2. Restore NuGet packages. The GUI uses experimental CommunityToolkit Labs packages; `CDPIUI/nuget.config` adds their [Azure feed](https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json) next to nuget.org, so a normal restore finds them.
3. Build `CDPI.sln` for **x64** (the only supported platform).
4. Run `CDPIUI.TrayIcon` **as administrator**, then run the GUI (`CDPIUI`) **as a normal user**. Some GUI features, such as dropping files from Explorer, do not work when the GUI itself is elevated.

Other useful commands:

- Unit tests: `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj` (on Linux or macOS add `-p:EnableWindowsTargeting=true`; the GUI project cannot be built there).
- Release layout: `pwsh .github/scripts/build_portable.ps1` builds the GUI, the single-file tray, `Update.exe` and `EditHostFile.exe` into `CDPIUI/bin/x64/Release/net8.0-windows10.0.22621.0/publish`.
- Installer: after `build_portable.ps1`, run `pwsh .github/scripts/check_setup_files.ps1` and `dotnet build NewSetup/NewSetup.wixproj -c Release -p:Platform=x64` ([WiX Toolset 6](https://wixtoolset.org/)). Every file of the publish folder must be listed in `NewSetup/NewSetupFiles.wxs`; the check script reports the differences.
- Patches (`.cdpipatch`) are built by the `build_and_patch.yml` workflow; see [.github/README.md](README.md).

CI (`.github/workflows/ci.yml`) builds the libraries and runs the tests, builds the GUI, checks the installer file list, builds `Setup.msi`, and builds `Update` and `EditHostFile`. Make sure it is green before asking for a review.

## Project structure

```text
cdpiui/
├── .github/                    # CI workflows and build/release scripts (see .github/README.md)
├── CDPIUI/                     # GUI (WinUI 3)
│   ├── Assets/                 # Images
│   ├── Commands/               # cdpiui:// links: CommandUriMapper and CommandsHandler
│   ├── ConditionalLaunch/      # Localized catalogues of ConditionalLaunch triggers and actions
│   ├── Controls/               # User controls; Controls/Dialogs holds content dialogs
│   ├── Converters/             # XAML value converters
│   ├── ELUA/                   # License agreement, one Markdown file per language (EN, RU)
│   ├── Extensions/             # GUI extension methods
│   ├── Help/                   # In-app help articles, one folder per language (EN, RU)
│   ├── Helper/                 # GUI helpers (code that needs localization or XAML types)
│   ├── Strings/                # UI localization (.resw), one folder per language (en-us, ru)
│   ├── Template/               # Files copied to the build output
│   ├── ViewModels/             # View models
│   ├── Views/                  # Pages; pages of one window live in a folder named after the window
│   ├── Windows/                # Windows (namespace CDPIUI, not CDPIUI.Windows)
│   ├── App.xaml(.cs)           # Application resources and startup
│   └── Program.cs              # Entry point, single instance handling
├── CDPIUI.AddOns/              # Internal add-ons, one folder per add-on
├── CDPIUI.Core/                # Application logic used by the GUI: components, presets, Store, LScript, proxy, updates
├── CDPIUI.Shared/              # Code shared by the GUI, Core and the tray: pipe, tasks, logging, settings, errors
├── CDPIUI.Tests/               # xUnit tests for Shared, Core and AddOns
├── CDPIUI.TrayIcon/            # Elevated tray application (WinForms)
├── EditHostFile/               # Elevated hosts file editor (.NET Framework 4.7.2)
├── NewSetup/                   # WiX 6 installer
├── Update/                     # Patch installer (.NET Framework 4.7.2)
├── CDPI.sln
├── LICENSE.txt
├── README.md                   # User-facing README (Russian)
└── THIRD-PARTY-NOTICES.md
```

## Changing the application

### General rules

- Keep changes focused. Do not change the entry points (`Program.cs` of each project), the `cdpiui://` handler or the pipe protocol without a reason; prefer adding new code over reworking code that works.
- Put new code in the folder that matches its role, so the structure stays understandable.
- Use correct English names. Names that are written to disk or sent over the pipe (settings keys, Store and preset JSON fields, enum values in files) must keep their stored value when the C# name changes: use `[JsonProperty]`/`[JsonPropertyName]` or a mapping, and read the old settings key when renaming one. Pipe message types are sent as numbers, so renaming enum members is safe, but reordering them is not.
- Anything the tray runs with administrator rights must come from the installation directory or a CDPIUI data directory and pass the checks in `CDPIUI.TrayIcon` (trusted paths, package signatures). Do not open files or links from the tray through the shell.
- Add unit tests to `CDPIUI.Tests` for logic in Shared, Core and AddOns.

### CDPIUI.TrayIcon

The tray must stay fast and responsive: it runs all the time and keeps the components running. Avoid blocking calls on its UI thread and keep its memory use low.

### CDPIUI.Shared

Shared is the common library of the GUI and the tray. Add something here only when both sides need it; code used only by the GUI belongs in Core or in the GUI project. Make sure a fix does not change behaviour that the other side relies on.

### CDPIUI.Core

Put new functionality in the folder that matches it (Store, ComponentServices, LScript, Proxy and so on). Note that the namespace `CDPIUI.Core.System` hides `System` inside Core; use `global::System` where needed.

### Add-ons

The application supports external and internal add-ons; both need some application code.

- **External add-ons** (for example GoodCheck) are installed from the Store and do their work in their own executable. The application needs code that configures the add-on, starts it through the tray and shows its results.
- **Internal add-ons** live entirely in `CDPIUI.AddOns`, one folder per add-on. The GUI adds pages, dialogs and localization for them.

`CDPIUI.AddOns` references `CDPIUI.Core` and `CDPIUI.Shared`, so an add-on can use their public types directly. See [CDPIUI.AddOns/README.md](../CDPIUI.AddOns/README.md).

### New components

Components, presets ("config kits") and subscriptions are Store items. You can request one in the developer's channels (links are in the application's Telegram channel) or add it to the [Store repository](https://github.com/Storik4pro/cdpiui-store) yourself.

A new component also needs application code: add its id to `CDPIUI.Core/Store/Data/HardcodedItemIds.cs` and its display name to `ComponentNames` in `CDPIUI.TrayIcon/Helper/Locale/LocaleHelper.cs` (component names are English only). A new preset kit or subscription needs no code.

## Localization

On the English UI a ready-made settings file of a component is called a "Config"; on the Russian UI it is a "Пресет".

To add a language:

1. Create the `.resw` files in `CDPIUI/Strings/<code>`, where `<code>` looks like `en-us` or `ru`, and add them to `CDPIUI.csproj` as `Content` with "Copy if newer".
2. Translate the license agreement into `CDPIUI/ELUA/<CODE>/ELUA.md` and the help articles into `CDPIUI/Help/<CODE>/`, where `<CODE>` is the Store language code (`EN`, `RU`).
3. Translate the Store texts in the [Store repository](https://github.com/Storik4pro/cdpiui-store).
4. Add a string with the language code as its key (for example `en-us`) and the language name as its value to the `Resources.resw` of every language, and add the language to `Languages` in `CDPIUI/Helper/UserExperience/LocalizationHelper.cs`.
5. Map the system culture to the new folder in `InitializeLocalizer` in `CDPIUI/App.xaml.cs`.
6. For the tray, add a dictionary to `CDPIUI.TrayIcon/Helper/Locale/LocaleHelper.cs` and a case for the two-letter ISO code in `GetLocaleString`.

## Documentation

| Part | Document |
|---|---|
| GUI | [CDPIUI/README.md](../CDPIUI/README.md) |
| Core | [CDPIUI.Core/README.md](../CDPIUI.Core/README.md), LScript: [CDPIUI.Core/LScript/README.md](../CDPIUI.Core/LScript/README.md) |
| Shared | [CDPIUI.Shared/README.md](../CDPIUI.Shared/README.md), pipe protocol: [CDPIUI.Shared/Pipe/README.md](../CDPIUI.Shared/Pipe/README.md) |
| Tray | [CDPIUI.TrayIcon/README.md](../CDPIUI.TrayIcon/README.md) |
| Add-ons | [CDPIUI.AddOns/README.md](../CDPIUI.AddOns/README.md) |
| Tests | [CDPIUI.Tests/README.md](../CDPIUI.Tests/README.md) |
| Updater | [Update/README.md](../Update/README.md) |
| Hosts editor | [EditHostFile/README.md](../EditHostFile/README.md) |
| Installer | [NewSetup/README.md](../NewSetup/README.md) |
| CI and releases | [.github/README.md](README.md) |
