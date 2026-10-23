**English** | [Русский](README.ru.md)

# CDPIUI.Tests

`CDPIUI.Tests` contains the xUnit tests for `CDPIUI.Shared`, `CDPIUI.Core` and `CDPIUI.AddOns`. The target framework is `net8.0`.

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

## Covered areas

| Folder | Test class | Subject |
| ------ | ---------- | ------- |
| `AddOns` | `ConfigImportServiceTests` | Parse of `set` commands in imported scripts |
| `AddOns` | `ConfigShareWriteOptionsTests` | Rejection of options that write outside a `.cdpiconfig` package |
| `AddOns` | `ZapretConfigImportServiceTests` | Batch file syntax: shell tail, carets, file modifiers |
| `Core` | `ArchivePathsTests` | Safe archive paths inside the target root |
| `Core` | `BatchFileEncodingTests` | Encoding of batch files (Cyrillic, UTF-8 BOM) |
| `Core` | `ConfigCommandLineTests` | Tokenization of preset command lines |
| `Core` | `ConfigFileReferencesTests` | File references in preset arguments (blob offsets, LUA files) |
| `Core` | `LScriptCoreTests` | LScript conditions, nested arguments and the parse of condition strings |
| `Core` | `ProcessServiceTests` | Quiet flag in process arguments |
| `Core` | `ProxyHelperTests`, `ProxySettingsTests` | Proxy address, port and format parse |
| `Core` | `ReplaceVariablesTests` | Variable replacement |
| `Core` | `VersionHelperTests` | Version comparison |
| `Core` | `Zapret1ToZapret2ConverterTests` | Conversion of Zapret Legacy arguments to Zapret2 |
| `Core` | `ZipServiceTests` | Merge of domain lists during ZIP extraction |
| `Shared` | `ApplicationPathMatcherTests` | Match of application file paths |
| `Shared` | `CatalogVerifierTests` | Signature check of update catalogs |
| `Shared` | `ConditionalTaskFileServiceTests` | Save and load of `.cdpitask` files |
| `Shared` | `ErrorConvertorTests` | Error code conversion |
| `Shared` | `GuiActivationArgumentsTests` | `--gui-args:` encode, decode and command-line quotes |
| `Shared` | `OutputCleanupHelperTests` | Removal of escape sequences from console output |
| `Shared` | `PipeMessageModelTests`, `PipePayloadCodecTests` | Pipe message round trip |
| `Shared` | `StreamStringTests` | Pipe stream strings |
| `Shared` | `TasksManageServiceTests` | Task creation and stop with concurrent calls |
| `Shared` | `TrustedPathValidatorTests` | Trusted path checks |
| `Shared` | `XmlSettingsServiceTests` | XML settings, values in different cultures |

## Test data

`TestData` contains the files for the tests. The project copies all files in `TestData` to the output directory (`<None Include="TestData\**" CopyToOutputDirectory="PreserveNewest" />`).

- `release-catalog.cat`: a catalog signed by the release certificate. `CatalogVerifierTests` uses it.

To read a test file, use `Path.Combine(AppContext.BaseDirectory, "TestData", "<file>")`.

## Add a test

1. Put the test class in the folder of the tested project: `AddOns`, `Core` or `Shared`.
2. Use the namespace `CDPIUI.Tests.<Folder>`.
3. Mark each test with `[Fact]` or `[Theory]`. The project imports `Xunit` globally.
4. If the test writes files, use a new directory in `Path.GetTempPath()`.
5. Delete this directory in `Dispose`.
6. If the test needs a data file, put it in `TestData`.
7. Run the tests.

Do not use the real application data directory or the installed settings in a test.

## Not covered

The tests do not cover the `CDPIUI` GUI project (WinUI 3). The test project does not reference it. Windows, pages, `CommandUriMapper`, `WindowOpenHelper`, the help parser and the localization need a manual check.

The tests also do not cover `CDPIUI.TrayIcon`, `EditHostFile`, `Update` and `NewSetup`.
