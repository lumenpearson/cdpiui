**English** | [Русский](README.ru.md)

# CDPIUI.Tests

`CDPIUI.Tests` contains the xUnit tests for `CDPIUI.Shared`, `CDPIUI.Core`, `CDPIUI.AddOns`, `Update` and `EditHostFile`. The target framework is `net8.0-windows`, because `Update` and `EditHostFile` target `net8.0-windows`. The project sets `DisableTransitiveFrameworkReferences`: the tests do not need the Windows Desktop runtime of `Update`, and they also run on Linux.

`CDPIUI.ElevatedLauncher` is a Native AOT executable. The project does not reference it. It compiles `CDPIUI.ElevatedLauncher/LaunchPolicy.cs` as a linked file (`Tools\ElevatedLauncher\LaunchPolicy.cs`).

## Run the tests

On Windows:

```
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj
```

On Linux or macOS:

```
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -p:EnableWindowsTargeting=true
```

Run the command from the repository root. The referenced projects use Windows packages. Thus a build on a different OS needs `EnableWindowsTargeting`.

The tests do not need the tray, the GUI, administrator rights or a network connection. The tests that write files use directories in `Path.GetTempPath()`.

## Coverage

`coverlet.collector` collects the coverage. The file `coverage.runsettings` in the repository root writes Cobertura and OpenCover reports and leaves out the tests and generated code. OpenCover also has the cyclomatic complexity of each method. Use it to find complex code without tests.

```
dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -p:DebugType=portable --settings coverage.runsettings
```

NOTE: `CDPIUI.Core`, `CDPIUI.Shared` and `CDPIUI.AddOns` build without debug symbols in Release (`DebugType` is `none`). Without `-p:DebugType=portable`, the report has only `Update` and `EditHostFile`.

The reports are in `TestResults/<id>/`. To get a readable report, use a report tool such as ReportGenerator.

## Covered areas

| Folder | Test class | Subject |
| ------ | ---------- | ------- |
| `AddOns` | `BlockCheckReportTests` | BlockCheck2 text reports: write and read back, invalid reports on import, site-list scope |
| `AddOns` | `BlockCheckTargetInputParserTests` | BlockCheck2 target list: schemes, ports, protocols, IDN, duplicates, limits |
| `AddOns` | `ConfigImportGoldenTests` | Import of Flowseal batch files compared with the expected files in `TestData\ConfigImport` |
| `AddOns` | `ConfigImportServiceTests` | Parse of `set` commands in imported scripts |
| `AddOns` | `ConfigShareReadTests` | Read of a `.cdpiconfig` package: valid and damaged packages, size limits |
| `AddOns` | `ConfigShareSafeDestinationTests` | Paths inside a `.cdpiconfig` package: traversal, device names, links |
| `AddOns` | `ConfigShareWriteOptionsTests` | Rejection of options that write outside a `.cdpiconfig` package |
| `AddOns` | `CurlBlockCheckProbeRunnerTests` | curl probe of BlockCheck2 with a fake process: the system curl path, arguments, exit codes, block pages, cancellation |
| `AddOns` | `StrategyCatalogValidatorTests` | Rules of the BlockCheck2 strategy catalogue and the built-in catalogue |
| `AddOns` | `ZapretConfigImportServiceTests` | Batch file syntax: shell tail, carets, file modifiers |
| `Core` | `BatchFileEncodingTests` | Encoding of batch files (Cyrillic, UTF-8 BOM) |
| `Core` | `ConfigCommandLineTests` | Tokenization of preset command lines |
| `Core` | `ConfigFileReferencesTests` | File references in preset arguments (blob offsets, LUA files) |
| `Core` | `ConfigItemContentTests` | Variables of presets: expansion, cycles, file references, stored form |
| `Core` | `DownloadFileNameTests` | Target file names of Store downloads: plain names only, no paths |
| `Core` | `DownloadHashTests` | SHA-256 check of Store downloads |
| `Core` | `IniDocumentTests` | Read and change of INI files with `IniDocument` |
| `Core` | `LScriptCoreTests` | LScript conditions, nested arguments and the parse of condition strings |
| `Core` | `ProcessServiceTests` | Quiet flag in process arguments |
| `Core` | `ProxyHelperTests`, `ProxySettingsTests` | Proxy address, port and format parse |
| `Core` | `StoreRequestCredentialsTests` | Credentials of Store requests: the repository token only in direct requests to the repository hosts, the proxy key only in proxy requests |
| `Core` | `ReplaceVariablesTests`, `ReplaceFilesPathTests` | Variable replacement and file path replacement in presets |
| `Core` | `VersionHelperTests` | Version comparison |
| `Core` | `Zapret1ToZapret2ConverterTests` | Conversion of Zapret Legacy arguments to Zapret2 (with the expected files in `TestData\Zapret1ToZapret2`) |
| `Core` | `ZipServiceTests` | Merge of domain lists during ZIP extraction |
| `Shared` | `ApplicationPathMatcherTests` | Match of application file paths |
| `Shared` | `ArchiveLimitsTests` | Archive limits: entry count, compression ratio, bounded extraction, links in the target root |
| `Shared` | `ArchivePathsTests` | Safe archive paths inside the target root |
| `Shared` | `CatalogVerifierTests` | Signature check of update catalogs |
| `Shared` | `ConditionalTaskFileServiceTests` | Save and load of `.cdpitask` files |
| `Shared` | `ErrorConvertorTests` | Error code conversion |
| `Shared` | `ExecutableLeaseTests` | Locked files and fingerprint of a program, the approval file, the Store item folder |
| `Shared` | `FileWritingOptionsTests` | Options in component arguments that write files outside the permitted folders |
| `Shared` | `GoodbyeDpiMigrationActivationTests` | Activation arguments of the GoodbyeDPI migration: round trip and invalid values |
| `Shared` | `GuiActivationArgumentsTests` | `--gui-args:` encode, decode and command-line quotes |
| `Shared` | `OutputCleanupHelperTests` | Removal of escape sequences from console output |
| `Shared` | `PipeMessageModelTests`, `PipePayloadCodecTests` | Pipe message round trip |
| `Shared` | `PipeSessionFileTests` | Pipe name and key format, write and read of the session file, stale and damaged files |
| `Shared` | `StreamStringTests` | Pipe stream strings |
| `Shared` | `TasksManageServiceTests` | Task creation and stop with concurrent calls |
| `Shared` | `TrustedPathValidatorTests` | Trusted path checks |
| `Shared` | `XmlSettingsServiceTests` | XML settings, values in different cultures |
| `Tools` | `ElevatedLauncherTests` | `CDPIUI.ElevatedLauncher`: allowed target names, removal of the .NET runtime variables, launcher arguments of `ElevatedLauncherCommand` |
| `Tools` | `EditHostFileTests` | `EditHostFile`: encoding detection, line breaks, add, remove and recover of the CDPI UI block, backup |
| `Tools` | `UpdateInstallHelperTests` | `Update`: path checks, layer sequence, skipped files (`catalog.cat`, `Update.exe`), apply and rollback |

## Test data

`TestData` contains the files for the tests. The project copies all files in `TestData` to the output directory (`<None Include="TestData\**" CopyToOutputDirectory="PreserveNewest" />`).

- `release-catalog.cat`: a catalog signed by the release certificate. `CatalogVerifierTests` uses it.
- `ConfigImport\<case>\`: Flowseal batch files and the expected import results (`*.zapret.expected.txt`, `*.import.expected.txt`). `ConfigImportGoldenTests` uses them.
- `Zapret1ToZapret2\`: Zapret Legacy arguments and the expected Zapret2 arguments. `Zapret1ToZapret2ConverterTests` uses them.

To read a test file, use `Path.Combine(AppContext.BaseDirectory, "TestData", "<file>")`. To read an expected-results file, use `GoldenFile.Load(...)`. In this file format, a line `[name]` starts a section. The lines after it are the values of the section. The class skips empty lines and lines that start with `#`.

## Add a test

1. Put the test class in the folder of the tested project: `AddOns`, `Core` or `Shared`. Put the tests of `Update`, `EditHostFile` and `CDPIUI.ElevatedLauncher` in `Tools`.
2. Use the namespace `CDPIUI.Tests.<Folder>`.
3. Mark each test with `[Fact]` or `[Theory]`. The project imports `Xunit` globally.
4. If the test writes files, use a new directory in `Path.GetTempPath()`.
5. Delete this directory in `Dispose`.
6. If the test needs a data file, put it in `TestData`.
7. Run the tests.

Do not use the real application data directory or the installed settings in a test.

## Not covered

The tests do not cover the `CDPIUI` GUI project (WinUI 3). The test project does not reference it. Windows, pages, `CommandUriMapper`, `WindowOpenHelper`, the help parser and the localization need a manual check.

The tests also do not cover `CDPIUI.TrayIcon` and `NewSetup`. For `Update`, the window, the stop of the application and the start of the tray need a manual check.
