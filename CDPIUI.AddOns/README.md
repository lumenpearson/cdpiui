# CDPIUI.AddOns

`CDPIUI.AddOns` is the library of the internal add-ons of CDPI UI. Each add-on has one folder. The GUI project `CDPIUI` contains the pages, the dialogs and the translations of the add-ons.

## Project

| Item | Value |
|---|---|
| Project file | `CDPIUI.AddOns/CDPIUI.AddOns.csproj` |
| Target framework | `net8.0` (not a Windows target, thus no WinUI types) |
| Project references | `CDPIUI.Core`, `CDPIUI.Shared` |
| Package references | `System.ServiceProcess.ServiceController` |
| Embedded resources | `BlockCheck2/Catalog/strategy-catalog.v1.json` |
| Internal types visible to | `CDPIUI.Tests` (`InternalsVisibleTo`) |
| Projects that refer to it | `CDPIUI`, `CDPIUI.Tests` |

The namespace of an add-on is `CDPIUI.AddOns.<Folder>`, for example `CDPIUI.AddOns.ConfigImport`.

## Add-ons

### BlockCheck2

BlockCheck2 finds Zapret2 strategies for a set of sites with `curl` probes. It combines the successful strategies into one multi-profile preset, checks the complete preset, and repairs it if necessary. It also keeps a history of reports and lets the user build and save a preset manually. The auto-selection window (`cdpiui://Tools/AutoConfig/Zapret2`) opens it for Zapret2. Refer to [BlockCheck2/README.md](BlockCheck2/README.md).

### ConfigImport

ConfigImport converts `.bat`, `.cmd`, `.txt` and `.json` files into a CDPI UI config with a static analysis. It never runs the source script. It has a special analyzer for `winws` and `winws2`, finds the missing resources, lets the user test the result and copies the resources into `LocalUserStorage`. It sends `.cdpiconfig` packages to ConfigShare. The import window (`cdpiui://Tools/ImportConfig`) uses it. Refer to [ConfigImport/README.md](ConfigImport/README.md).

### ConfigShare

ConfigShare exports one preset with its resources into a `.cdpiconfig` ZIP archive. It checks, extracts and installs such archives. It limits the archive size, the entry count and the compression ratio, and it rejects options that write files outside the package. The export dialog of `ConfigSelector` and the file activation use it. Refer to [ConfigShare/README.md](ConfigShare/README.md).

### GoodCheck

GoodCheck is the CDPI UI side of the external GoodCheck add-on from the Store. It prepares the site lists, sends a start request to the tray, reads the GoodCheck logs and writes an XML report. It supports only ByeDPI (`ciadpi`), GoodbyeDPI (`gdpi`) and Zapret Legacy (`zapret`), in the modes `AllAsOne` and `AnyAsAny`. The auto-selection window opens it for these components. Refer to [GoodCheck/README.md](GoodCheck/README.md).

### HostsFileEditor

HostsFileEditor starts `EditHostFile.exe` with administrator rights to add, remove or recover the CDPI UI block in the Windows hosts file. It returns the exit code of the tool. The hosts file window (`cdpiui://Tools/Hosts`) uses it. Refer to [HostsFileEditor/README.md](HostsFileEditor/README.md).

### Troubleshooting

Troubleshooting checks Windows services, network settings and programs that can stop a DPI bypass. It also checks the access to the Store repositories and the Store folders. It can fix the TCP timestamps, the WinDivert service and the Store folder access. The troubleshooting window (`cdpiui://Tools/Troubleshooting`) uses it. Refer to [Troubleshooting/README.md](Troubleshooting/README.md).

## Add a new internal add-on

### Logic in `CDPIUI.AddOns`

1. Make a folder `CDPIUI.AddOns/<Name>`.
2. Use the namespace `CDPIUI.AddOns.<Name>` and its sub-namespaces.
3. Put only logic without UI into the folder. The project targets `net8.0` and cannot use WinUI or XAML types.
4. Use the public types of `CDPIUI.Core` and `CDPIUI.Shared` directly. Do not add a reference to the `CDPIUI` project.
5. Add an `EmbeddedResource` item to `CDPIUI.AddOns.csproj` for each file that the add-on embeds. The SDK includes `.cs` files automatically.
6. Write a `README.md` in the folder. Add a paragraph and a link to this file.

Use these rules:

- Give the UI an entry point that accepts its dependencies as parameters, for example `BlockCheckAddOnService`.
- Put the code that starts processes behind an interface, for example `IBlockCheckStrategyRunner`. Then tests can use a fake implementation.
- Return stable issue or error codes. Do not return translated texts.
- Start components and external add-on executables through the tray, for example with `ComponentTasksManager` or a pipe message. The tray starts only files that pass its trusted path checks. The GUI process can start system tools itself, for example `curl.exe` in BlockCheck2.

WARNING: The tray operates as administrator. Do not send a path or arguments from an untrusted file to the tray without a check. For example, ConfigShare and BlockCheck2 reject options that write files.

### GUI pieces in `CDPIUI`

| Piece | Folder |
|---|---|
| Window | `CDPIUI/Windows` |
| Pages of the window | `CDPIUI/Views/<WindowName>` |
| User controls | `CDPIUI/Controls/<Name>` |
| Content dialogs | `CDPIUI/Controls/Dialogs/<Name>` |
| View models | `CDPIUI/ViewModels` |
| GUI helpers that use translations or XAML types | `CDPIUI/Helper/AddOns/<Name>` |
| UI strings | `CDPIUI/Strings/en-us/Resources.resw` and `CDPIUI/Strings/ru/Resources.resw` |
| Help articles | `CDPIUI/Help/EN` and `CDPIUI/Help/RU` |
| `cdpiui://` routes | `CDPIUI/Commands/CommandUriMapper.cs` |
| Entry card on the tools page | `CDPIUI/Views/UtilsPage.xaml(.cs)` |

### Tests

Add unit tests to `CDPIUI.Tests/AddOns`. `CDPIUI.Tests` already refers to `CDPIUI.AddOns` and can see its internal types.

### External add-ons

An external add-on, for example GoodCheck, is an executable from the Store. Its ID goes to `HardcodedItemIds.AddOnsIds` in `CDPIUI.Core/Store/Data/HardcodedItemIds.cs`. The code in `CDPIUI.AddOns` sets it, starts it through the tray and reads its results.
