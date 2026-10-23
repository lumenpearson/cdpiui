**English** | [Русский](CONTRIBUTING.ru.md)

# Contributing to CDPI UI

This document tells you how the application operates, how to build it and how to change it.
Each project has a README with more data. The list of these documents is at the end of this page.

## How the application operates

CDPI UI has two processes. The two processes send messages to each other through a named pipe.

| Part | Projects | User account | Function |
|---|---|---|---|
| GUI | `CDPIUI`, `CDPIUI.AddOns`, `CDPIUI.Core`, `CDPIUI.Shared` | The signed-in user, without elevation | Shows the windows. Controls the Store, the presets, the add-ons and the settings. |
| Tray | `CDPIUI.TrayIcon`, `CDPIUI.Shared` | Administrator | Starts and stops the components. Controls the system proxy, the autorun, the notifications, Conditional Launch and the MSI installation. |

Two more tools use .NET 8. Each tool is a framework-dependent single-file executable, as the tray is:

- `Update` applies the application patches.
- `EditHostFile` changes the hosts file with administrator rights.

The `NewSetup` project makes the MSI installer.

The GUI does not start the components. The GUI sends a request to the tray. The tray starts the component and sends the process state and the output to the GUI.

When the GUI starts and the tray does not operate, the GUI starts the tray and stops. Then the tray starts the GUI again with the initial arguments.
If `APPEARANCE/hideToTrayOnStartup` is `true` and the GUI has no arguments, the tray does not start the GUI again.

## Build the application

You must have these items:

- Windows 10 or Windows 11, x64.
- Visual Studio 2022 with the .NET desktop and WinUI workloads. Alternatively, the .NET 8 SDK and MSBuild from Visual Studio 2022.
- PowerShell 7.

Do these steps:

1. Make the secrets class `CDPIUI.Shared/Secrets/Secret.cs`.
   - The class must be `public static class Secret` in the namespace `CDPIUI.Shared.Secrets`.
   - The class must have the members of the `ISecret` interface in [SecretExample.cs](../CDPIUI.Shared/Secrets/SecretExample.cs).
   - Copy only the members. Do not copy the interface, because it is already in that file.
   - For a local build without Store access, run `pwsh .github/scripts/create_secret_stub.ps1`. The script makes the class with empty tokens.
   - Git ignores this file. Do not add it to the repository.
2. Restore the NuGet packages.
   - The GUI uses CommunityToolkit Labs packages.
   - `CDPIUI/nuget.config` adds the [Labs feed](https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json) and nuget.org. A usual restore finds all packages.
3. Build `CDPI.sln` for the `x64` platform. The application does not support other platforms.
4. Start `CDPIUI.TrayIcon` as administrator.
5. Start the GUI (`CDPIUI`) as a usual user.

CAUTION: Do not start the GUI as administrator. Some GUI functions do not operate with elevation. For example, you cannot drop files from Explorer into an elevated window.

### Other commands

| Task | Command |
|---|---|
| Run the unit tests | `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj` |
| Run the unit tests on Linux or macOS | `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -p:EnableWindowsTargeting=true` |
| Build the release layout | `pwsh .github/scripts/build_portable.ps1` |
| Compare the installer file list with the release layout | `pwsh .github/scripts/check_setup_files.ps1` |
| Build the MSI installer | `dotnet build NewSetup/NewSetup.wixproj -c Release -p:Platform=x64` |

Some notes about these commands:

- You cannot build the GUI project on Linux or macOS.
- `build_portable.ps1` puts the GUI, the single-file tray, `Update.exe` and `EditHostFile.exe` into `CDPIUI/bin/x64/Release/net8.0-windows10.0.22621.0/publish`.
- Build the MSI installer after `build_portable.ps1`. The installer uses [WiX Toolset 6](https://wixtoolset.org/).
- `NewSetup/NewSetupFiles.wxs` must list all files of the release layout. `check_setup_files.ps1` shows the differences.
- The `build_and_patch.yml` workflow makes the patches (`.cdpipatch`). Refer to [.github/RELEASES.md](RELEASES.md).

### Continuous integration

The workflow `.github/workflows/ci.yml` does these checks:

- It builds the libraries, runs the unit tests and checks the NuGet packages for known vulnerabilities (job `libraries`).
- It builds the GUI and the release layout, compares the installer file list with the release layout and builds `Setup.msi` (job `gui`).
- It builds `Update` and `EditHostFile` and publishes each tool as a single file (job `tools`).

The `libraries` and `tools` jobs must pass before you ask for a review. The `gui` job has `continue-on-error`. Its result gives only information. Refer to [.github/RELEASES.md](RELEASES.md).

## Project structure

```text
cdpiui/
├── .github/                    # CI workflows and build scripts. Refer to .github/RELEASES.md.
├── CDPIUI/                     # The GUI (WinUI 3)
│   ├── Assets/                 # Images
│   ├── Commands/               # The cdpiui:// links: CommandUriMapper and CommandsHandler
│   ├── ConditionalLaunch/      # Translated lists of Conditional Launch triggers and actions
│   ├── Controls/               # User controls. Controls/Dialogs contains the content dialogs.
│   ├── Converters/             # XAML value converters
│   ├── ELUA/                   # The license agreement. One Markdown file for each language (EN, RU).
│   ├── Extensions/             # Extension methods of the GUI
│   ├── Help/                   # The help articles. One folder for each language (EN, RU).
│   ├── Helper/                 # GUI helpers that use translations or XAML types
│   ├── Strings/                # The UI translations (.resw). One folder for each language (en-us, ru).
│   ├── Template/               # Files that the build copies to the output folder
│   ├── ViewModels/             # View models
│   ├── Views/                  # Pages. The pages of one window are in a folder with the window name.
│   ├── Windows/                # Windows. The namespace is CDPIUI, not CDPIUI.Windows.
│   ├── App.xaml(.cs)           # Application resources and start-up
│   └── Program.cs              # Entry point. Controls the single instance.
├── CDPIUI.AddOns/              # Internal add-ons. One folder for each add-on.
├── CDPIUI.Core/                # Application logic of the GUI: components, presets, Store, LScript, proxy, updates
├── CDPIUI.Shared/              # Code for the GUI, Core and the tray: pipe, tasks, logs, settings, errors
├── CDPIUI.Tests/               # xUnit tests for Shared, Core, AddOns, Update and EditHostFile
├── CDPIUI.TrayIcon/            # The tray application (WinForms). It operates as administrator.
├── EditHostFile/               # The hosts file editor (.NET 8, console). It operates as administrator.
├── NewSetup/                   # The WiX 6 installer
├── Update/                     # The patch installer (.NET 8, WinForms)
├── CDPI.sln
├── LICENSE.txt
├── README.md                   # The README for users (in Russian)
└── THIRD-PARTY-NOTICES.md
```

## Change the application

### General rules

- Make small changes that have one purpose.
- Do not change these items without a good reason: the entry points (`Program.cs` of each project), the `cdpiui://` handler and the pipe protocol.
- If possible, add new code. Do not change code that operates correctly.
- Put new code in the folder for its function.
- Use correct English names.
- Some names go to the disk or through the pipe. Examples: settings keys, Store JSON fields, preset JSON fields and enum values in files. If you change the C# name, you must keep the stored value. Use `[JsonProperty]`, `[JsonPropertyName]` or a map. If you change a settings key, read the old key too.
- The pipe sends message types by name (`cdpiui://<Target>/<MessageType>`). If you change the name of a pipe enum member, you change the protocol. The GUI and the tray must then come from the same build. The other side cannot read a message with an unknown name.
- Add unit tests to `CDPIUI.Tests` for the logic in Shared, Core, AddOns, `Update` and `EditHostFile`.

WARNING: The tray operates as administrator. Run files only from the installation folder or a CDPIUI data folder. Make sure that the files pass the checks in `CDPIUI.TrayIcon` (trusted paths, executable trust in `ExecutableTrust`, package signatures). Do not open files or links through the shell from the tray.

### CDPIUI.TrayIcon

The tray operates all the time and keeps the components in operation. The tray must be fast and it must always respond.

- Do not block the UI thread of the tray.
- Keep the memory use of the tray low.

### CDPIUI.Shared

Shared is the common library of the GUI and the tray.

- Add code to Shared only when the GUI and the tray both use it.
- Put code for the GUI only in Core or in the GUI project.
- Before you change Shared, make sure that the other process does not use the old behavior.

### CDPIUI.Core

- Put new functions into the applicable folder, for example `Store`, `ComponentServices`, `LScript` or `Proxy`.
- The namespace `CDPIUI.Core.System` hides the namespace `System` in Core. Use `global::System` when necessary.

### Add-ons

The application has external add-ons and internal add-ons. Both types need some application code.

| Type | Location of the logic | Application code |
|---|---|---|
| External (for example GoodCheck) | An executable file from the Store | Code that sets the add-on, starts it through the tray and shows the results |
| Internal | A folder in `CDPIUI.AddOns` | Pages, dialogs and translations in the GUI |

`CDPIUI.AddOns` has references to `CDPIUI.Core` and `CDPIUI.Shared`. An add-on can use their public types directly. Refer to [CDPIUI.AddOns/README.md](../CDPIUI.AddOns/README.md).

### Add a component

Components, preset packs and subscriptions are Store items.

- To ask for a new item, use the developer channels. The application Telegram channel has the links.
- To add an item yourself, use the [Store repository](https://github.com/Storik4pro/cdpiui-store).

A new component also needs application code:

1. Add the component ID to `CDPIUI.Core/Store/Data/HardcodedItemIds.cs`.
2. Add the component name to `ComponentNames` in `CDPIUI.TrayIcon/Helper/Locale/LocaleHelper.cs`. Component names are only in English.

A new preset pack or a new subscription does not need code.

## Translations

The developer documents use the term "preset" for a ready settings file of a component. The English UI uses the word "Config" for a preset. The Russian UI uses the word "Пресет".

To add a language, do these steps:

1. Make the `.resw` files in `CDPIUI/Strings/<code>`. The code has the format `en-us` or `ru`.
2. Add the `.resw` files to `CDPIUI.csproj` as `Content` with `PreserveNewest`.
3. Translate the license agreement into `CDPIUI/ELUA/<CODE>/ELUA.md`. `<CODE>` is the Store language code, for example `EN` or `RU`.
4. Translate the help articles into `CDPIUI/Help/<CODE>/`.
5. Translate the Store texts in the [Store repository](https://github.com/Storik4pro/cdpiui-store).
6. In the `Resources.resw` file of each language, add a string. The key is the language code. The value is the name of the language.
7. Add the language to `Languages` in `CDPIUI/Helper/UserExperience/LocalizationHelper.cs`.
8. In `InitializeLocalizer` in `CDPIUI/App.xaml.cs`, add the system culture of the new language.
9. For the tray, add a dictionary to `CDPIUI.TrayIcon/Helper/Locale/LocaleHelper.cs`.
10. In `GetLocaleString` of the same file, add a case for the two-letter ISO code.

## Documents

| Part | Document |
|---|---|
| GUI | [CDPIUI/README.md](../CDPIUI/README.md) |
| Core | [CDPIUI.Core/README.md](../CDPIUI.Core/README.md) |
| LScript language | [CDPIUI.Core/LScript/README.md](../CDPIUI.Core/LScript/README.md) |
| Shared | [CDPIUI.Shared/README.md](../CDPIUI.Shared/README.md) |
| Pipe protocol | [CDPIUI.Shared/Pipe/README.md](../CDPIUI.Shared/Pipe/README.md) |
| Tray | [CDPIUI.TrayIcon/README.md](../CDPIUI.TrayIcon/README.md) |
| Add-ons | [CDPIUI.AddOns/README.md](../CDPIUI.AddOns/README.md) |
| Tests | [CDPIUI.Tests/README.md](../CDPIUI.Tests/README.md) |
| Updater | [Update/README.md](../Update/README.md) |
| Hosts file editor | [EditHostFile/README.md](../EditHostFile/README.md) |
| Installer | [NewSetup/README.md](../NewSetup/README.md) |
| CI and releases | [.github/RELEASES.md](RELEASES.md) |
