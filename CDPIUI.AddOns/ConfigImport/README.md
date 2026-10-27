**English** | [Русский](README.ru.md)

# Config Import add-on

## About this document

This document describes the import of config files in CDPI UI. The import starts with a static analysis of the source file. It ends with a test run and a saved `ConfigItem` in `LocalUserStorage`.

The main logic is in the namespace `CDPIUI.AddOns.ConfigImport`. The import wizard is in the `CDPIUI` project. The wizard is a client of the add-on.

This document is for developers and for the authors of the user help. It describes the current behavior, the limits and the cases where the result depends on the selected component or on a decision of the user.

## Functions

The add-on does these tasks:

1. It imports `.bat`, `.cmd`, `.txt` and `.json` files. It does not run the source script.
2. It finds the applicable installed component from its `executable` and from the file contents.
3. It converts the command line and the variables of the source file into a CDPI UI `ConfigItem`.
4. It uses a special static analyzer for `winws` and `winws2`.
5. It keeps editable BAT variable values in `variables`, `commaVars`, `availableCommaVarsValues` and `jparams`.
6. It finds external BIN, TXT and LUA resources, generated files and missing dependencies.
7. It suggests a replacement for a missing file. The user can also select an empty file.
8. It lets the user test the selected component with the imported arguments.
9. It copies the dependencies in the folder of the imported file to separate folders: `List/Converted`, `Bin/Converted` and `Lua/Converted`.
10. It converts a ready `winws1` command line to `winws2` when the user moves the config to Zapret 2.
11. It sends `.cdpiconfig` packages to the ConfigShare add-on. Refer to [ConfigShare/README.md](../ConfigShare/README.md).

## Safety principle

The import is static. `ConfigImportService` and `ZapretConfigImportService` read the source text, find known constructs and make a result model. They do not start `cmd.exe`, PowerShell, the source BAT or CMD file or the commands in it.

Thus the analysis does not:

- run `if`, `for`, `goto`, `copy`, `del`, `reg`, `netsh` or other shell commands;
- start called BAT files as processes;
- start programs from the source script;
- send network requests;
- change the source folder.

The installer copies only the resources in the folder of the imported file and its subfolders. A config can name any file that the user can read, and the user can share the preset later. Thus the installer does not copy a resource outside this folder. Refer to [Relative paths](#relative-paths).

The **Test** button is a separate operation. It starts the selected installed component through `ComponentTasksManager` with the calculated arguments.

WARNING: The test run is not a sandbox. The safety of the static import does not apply to the test run of the component executable.

## Supported input formats

| Format | General component | `winws` / Zapret Legacy | `winws2` / Zapret 2 |
|---|---:|---:|---:|
| `.bat` | Yes | Yes, special analyzer | Yes, special analyzer |
| `.cmd` | Yes | Yes, special analyzer | Yes, special analyzer |
| `.txt` | Yes | Yes, special analyzer | Yes, special analyzer |
| `.json` | Yes | General JSON import | General JSON import |
| `.cdpiconfig` | ConfigShare | ConfigShare | ConfigShare |

For Zapret, the special analyzer reads only BAT, CMD and TXT. The general `ConfigItem` and `OldConfigItem` loader reads JSON. `ConfigShareService` reads `.cdpiconfig` packages.

## Implementation

### Add-on

| File | Function |
|---|---|
| `ConfigImportModels.cs` | Common contracts: result, target, issues, missing and generated files |
| `ConfigImportService.cs` | Import facade, target detection, general parser, call of the Zapret parser, retarget |
| `ZapretConfigImportModels.cs` | Zapret models and analyzer options |
| `ZapretConfigImportService.cs` | Static analysis of BAT, CMD and TXT for `winws` and `winws2` |
| `ConfigImportAutoCorrector.cs` | Search for a safe replacement of a missing resource |
| `ConfigImportTestPlaceholder.cs` | Temporary files and path replacement before a test |
| `ConfigImportInstaller.cs` | Copies the resources, changes the paths and saves the config |

### CDPI UI integration

| File | Function |
|---|---|
| `CDPIUI/Views/ConfigImportUtil/MainPage.xaml` | Layout of the import wizard |
| `CDPIUI/Views/ConfigImportUtil/MainPage.xaml.cs` | Controls the analysis, the corrections, the test and the save |
| `CDPIUI/ViewModels/ConfigImportPresetViewModel.cs` | State of one imported config |
| `CDPIUI/ViewModels/ConfigImportMissingFileViewModel.cs` | State of the decision for one missing file |
| `CDPIUI/Helper/AddOns/ConfigImport/ConfigImportHelper.cs` | The file selection dialog |
| `CDPIUI/Windows/ConfigImportUtilWindow.*` | Separate window of the tool |
| `CDPIUI/Views/UtilsPage.*` | Entry point on the tools page |
| `CDPIUI/Commands/CommandUriMapper.cs` | Routes `cdpiui://Tools/ImportConfig` and `cdpiui://Tools/ImportConfig/{componentId}` |

## General sequence

```text
The user selects one or more files
        |
        v
Load the installed components from the Store DB
        |
        v
Find the target by componentId / executable / TXT signatures
        |
        v
Static import
   |                 |
   | general         | winws / winws2
   v                 v
ConfigImportService  ZapretConfigImportService
        |                 |
        +--------+--------+
                 v
          ConfigImportResult
                 |
                 v
Find missing files and ask for a decision
                 |
                 v
The user selects the component and the name
          |                    |
          v                    v
        Test                 Save
          |                    |
  temporary files      List/Bin/Lua/Converted
          |                    |
 start the component    config JSON in
                          LocalUserStorage
```

## Target model

`ConfigImportTarget` contains:

- `ComponentId`: the unique component ID in the Store DB;
- `DisplayName`: the display name;
- `Executable`: the executable name from the DB, with or without `.exe`;
- `Version`: the current component version;
- `Directory`: the folder of the installed component.

The wizard shows only Store items of the type `component` that meet these conditions:

1. `Id`, `Directory` and `Executable` are set.
2. The folder exists.
3. The executable exists in the folder. If necessary, the wizard adds the extension `.exe`.

Thus a component that is not installed does not take part in the automatic detection. The wizard cannot test or save a config for it.

## Automatic component detection

`FindMatchingTargets` operates as follows.

### `.cdpiconfig`

The method reads the package with `ConfigShareService.Read`. It selects the target with the component ID from `target[0]` of the packaged config. If the package is not valid, the method returns no target. The import then reports the detailed archive error.

### JSON

The method loads a `ConfigItem` from the JSON. It compares the first `target` element with the `ComponentId` values of the installed components.

### BAT, CMD and TXT

The method looks for a reference to the executable of each installed component. The name can have `.exe` or not. The form `%~dp0<executable>` is also valid.

The match uses name boundaries. For example, `winws` does not match a part of a longer identifier.

### More TXT signatures

If a TXT file has no explicit executable, the method uses these signatures:

- `# BuiltinVersion: ...` or the option `--lua-desync` means `winws2`;
- the option `--dpi-desync` means `winws`.

### Target priority in the wizard

The wizard selects the target for the analysis in this sequence:

1. the first target that the analyzer found;
2. the component ID from the route `cdpiui://Tools/ImportConfig/{componentId}`;
3. the first installed component in the list;
4. an empty target, if no component is applicable.

For the initial selection in the UI, the component from the route has priority, also when the analyzer found a different target. The wizard records the automatic detection separately. It uses this record for the warning about an incompatible selection.

## Analysis result

`ConfigImportResult` contains:

- `SharedPackage`: the extracted `.cdpiconfig` package, or `null`;
- `Config`: the `ConfigItem`, or `null` if the analysis failed;
- `Target`: the target of the analysis;
- `SourcePath`: the main imported file;
- `Issues`: the warnings and the errors;
- `SourceFiles`: the main file and the included config files and scripts;
- `ReferencedFiles`: the resources that exist. The installer copies only the resources in the folder of `SourcePath` and its subfolders;
- `MissingReferencedFiles`: the missing resources;
- `MissingFileResolutions`: the explicit decisions of the user;
- `GeneratedFiles`: the files that the source BAT makes before the launch.

The result is successful only if these two conditions are true:

```text
Config != null
and
Issues contains no item with Severity == Error
```

Warnings do not block the test and the save. Errors block the two operations.

## General component import

The general analyzer applies to all executables except the normalized names `winws` and `winws2`.

The normalization of the executable name removes the folder, the quotes and the end `.exe`. The comparison ignores the case.

### BAT and CMD import

1. The analyzer reads the file with the encoding detection of the Zapret analyzer (`BatchFileEncoding`): UTF-8, OEM 866 or ANSI 1251.
2. It joins physical lines that end with an odd number of `^` into one logical line.
3. It processes the assignments `set NAME=VALUE` and `set "NAME=VALUE"`, with an optional `@` before `set`.
4. It uses a `set` after `rem ` or `::` as an alternative value of the variable.
5. It skips comments and empty lines. A comment is a line that starts with `::` or `#`, or a line that starts with `rem` or `@rem` followed by a space, a tab or the line end.
6. It splits each line into tokens. A double quote starts or ends a quoted part.
7. It looks for a token that is the executable of the selected component after the static expansion.
8. The text after the executable becomes the argument line. A redirection, a pipe or `&` outside quotes cuts the rest of the line.
9. For each launch, the analyzer keeps a copy of the variables at the time of the launch.
10. Exactly one applicable launch must exist. If there is no launch or more than one launch, the import fails.

If the launch arguments are one token that points to an existing `.txt` file, the analyzer imports this file as the config contents. The token can have the prefix `@` or `$`. The analyzer resolves the path relative to the BAT or CMD file.

After it finds the arguments, the analyzer collects the used variables transitively. For example, if the startup string uses `%A%` and the value of `A` uses `%B%`, the result contains the two variables.

### General TXT import

1. The analyzer reads the file with the same logical line reader.
2. It skips empty lines and comments.
3. If a line contains a launch of the selected executable, the analyzer removes the executable and keeps only the arguments.
4. It joins the other lines with a space.
5. It takes the name from a comment `# Config:`, `# Preset:` or `# ActivePreset:` (`=` is also valid). Without such a comment, it uses the file name without the extension.

An empty argument set is an error.

### JSON import

The analyzer first tries to load a current `ConfigItem`. If the JSON has no `startup_string`, the analyzer tries to load an old `OldConfigItem`:

- `custom_parameters` goes to `startup_string`;
- `meta` becomes `pUC:v1.0`;
- the name comes from the file name;
- `jparams` and `variables` are empty.

After the load, the analyzer always sets `target` to the selected component ID and its version.

### Variables of the general importer

All used BAT variables go to `commaVars`. The main value gets these changes:

```text
%~dp0  ->  $GETCURRENTDIR()/
\      ->  /
```

If a variable has commented alternatives, the analyzer makes an `availableCommaVarsValues` item:

```json
{
  "VarName": "LISTS",
  "CurrentValueIndex": 0,
  "Values": [
    "$GETCURRENTDIR()/lists/default.txt",
    "$GETCURRENTDIR()/lists/alternative.txt"
  ]
}
```

An unknown `%VARIABLE%` in the startup string or in the values goes to `commaVars` with the value `$EMPTY`. The built-in variables `CD`, `ERRORLEVEL`, `DATE` and `TIME` are exceptions.

The general analyzer does not make conditional `jparams`. The `jparams` and `variables` of such a config are empty.

## Zapret import

`ZapretConfigImportService` does the static import for `winws` and `winws2`. In the code, the generations are `Winws1` and `Winws2`.

The Zapret import does not convert winws1 syntax to winws2. It makes a config of the generation that it finds in the source BAT, CMD or TXT. Only `ConfigImportService.Retarget` converts `winws1 -> winws2`, at a later step.

### Encoding of Zapret files

The analyzer reads BAT, CMD and TXT files through `CDPIUI.Core.System.BatchFileEncoding`. It finds the encoding in this sequence:

1. UTF-8 with BOM;
2. UTF-16 LE with BOM;
3. UTF-16 BE with BOM;
4. strict UTF-8 without BOM;
5. a Cyrillic code page if the bytes are not valid UTF-8. If more bytes are in the range `0x80`–`0xAF` than in the range `0xC0`–`0xFF`, the code page is OEM 866 (the `cmd` code page). Otherwise, it is ANSI 1251.

Thus the analyzer can import Russian BAT sets without a manual conversion.

### Zapret BAT and CMD analysis

The analyzer does these steps:

1. It reads the logical lines. A line that ends with an odd number of `^` continues on the next line.
2. It loads `PredefinedVariables`, if the caller supplies them.
3. It collects the usual and the commented `set` commands in sequence.
4. It follows the known calls of external BAT and CMD files statically.
5. It finds the `winws` or `winws2` launches. The executable name must not continue a longer name: `mywinws.exe` is not a launch.
6. It keeps a copy of the variables for each launch.
7. It requires exactly one launch.
8. It expands a referenced TXT config such as `@config.txt`.
9. It makes sure that the executable and the option generation agree.
10. It keeps only the variables that the launch uses.
11. It makes switches for conditional values and for `ACTIVE_*.bin`.
12. It finds the existing, missing and generated resources.
13. It makes the `ConfigItem`.

A comment is a line that starts with `rem`, `@rem` or `::`. After `rem`, a space, a tab or the line end must follow. A commented alternative value must start with `rem ` or `::`. The analyzer does not read `@rem set ...` as an alternative value.

The analyzer finds a launch by the text `winws` or `winws2`, with an optional `.exe`, before a quote, a space or the line end. After the executable, one of these items must follow: options that start with `--`, a referenced TXT file or a BAT variable. This rule decreases false matches with service lines.

The analyzer cuts the command line at the first `>`, `<`, `|` or `&` outside quotes. Outside quotes, it also removes caret escapes as `cmd` does: `^&` becomes `&`, and `^^` becomes `^`.

A backslash is not an escape character in the analyzer. Thus a quoted folder that ends with a backslash, for example `"C:\dir\"`, closes the quote correctly.

### Supported BAT variables

The static expansion knows these forms:

- `%~<modifiers>0`: the path of the current BAT file with the modifiers `f` (full path), `d` (drive), `p` (folder), `n` (name) and `x` (extension), combined as `cmd` does. For example, `%~dp0` is the folder of the BAT file, and `%~n0` is its name without the extension;
- `%0`: the full path of the BAT file;
- `%%`: a literal percent sign. The expansion does not read it as the start of a variable;
- `%NAME%`: a known BAT variable or an environment variable of the CDPI UI process;
- `!NAME!`: delayed expansion. The config gets `%NAME%` instead;
- `$GETCURRENTDIR()/` and `$GETCURRENTDIR()\` in values that are already normalized.

The recursive expansion has a maximum depth of 16. An unknown variable stays in the line, and the analyzer adds a warning.

### External BAT and CMD files and `service.bat`

The analyzer knows calls such as:

```bat
call "path\script.bat" argument
call path\script.cmd argument
```

A call of a local label, `call :label`, is not an external file. For an existing BAT or CMD file, the analyzer does these steps:

1. It resolves the path relative to the calling script.
2. If the path has no extension, it adds `.bat`.
3. It adds the file to `SourceFiles`.
4. It collects the variables of the file statically.
5. It merges the result with the variables of the calling file.

If the first argument of the call matches a construct such as `if "%~1"=="argument" (...)`, the analyzer reads the `call :label` labels in this block. If it does not find such a dispatch, it reads the top-level lines before the first BAT label.

`MaxIncludeDepth` limits the recursive calls. The default value is 8. The analyzer does not read the same file with the same arguments a second time.

The command `load_user_lists` has special support. In the label with this name in the external script, the analyzer reads these operations:

```bat
echo content > path\file.txt
echo content >> path\file.txt
```

If the target file does not exist, the analyzer collects its contents in memory and returns them in `GeneratedFiles`. The source folder does not change. The analyzer ignores a file outside the source folder and adds a warning.

This is a special static model, not a general BAT interpreter. The analyzer does not repeat other ways to make files.

### TXT configs and response files

A TXT config contains:

- comments that start with `#`;
- lines with options. The analyzer joins them with a space.

The analyzer knows this metadata:

```text
# BuiltinVersion: 2.11
# Preset: Config name
# ActivePreset: Config name
```

The version line can also have the forms `Built-in version` or `Builtin Version`, with `:` or `=`. `BuiltinVersion` means the winws2 generation and also sets the target version. If the file has no name metadata, the analyzer uses the file name.

The analyzer reads a reference `@file.txt` as a response file only if:

1. the file exists;
2. the extension is `.txt`;
3. one of the first 64 lines contains `BuiltinVersion` or starts with `--`.

If the response file does not exist, the analyzer adds the error `INCLUDED_CONFIG_NOT_FOUND`.

The contents of the response file replace the complete option line. If the line had other arguments near `@file.txt`, the analyzer ignores them and adds the warning `CONFIG_INCLUDE_IGNORES_OTHER_ARGUMENTS`.

`MaxIncludeDepth` (default 8) limits the depth of includes. The analyzer keeps a stack of the active files to find cycles.

### Zapret generation

The analyzer finds the generation in this sequence:

1. `BuiltinVersion` means winws2.
2. winws2 options mean winws2. These options are `--lua-init`, `--lua-desync`, `--blob`, `--out-range`, `--ctrack-*` and `--wf-tcp-out`, `--wf-tcp-in`, `--wf-udp-out`, `--wf-udp-in`.
3. `--dpi-desync` or the old `--wf-tcp` and `--wf-udp` mean winws1.
4. If a TXT file has no clear signs, the analyzer uses `DefaultTextConfigGeneration`.

The facade sets the default to the generation of the selected target. For a direct call of `ZapretConfigImportService`, the default is winws2. A `null` value disables the default.

If the BAT file starts one generation and the options or the included TXT clearly belong to the other generation, the import fails with `GENERATION_MISMATCH`.

### Version

For winws2, the analyzer selects the version in this sequence:

1. the value of `# BuiltinVersion`, if it exists;
2. `CurrentVersionResolver` from `ZapretConfigImportOptions`;
3. `CurrentVersion` of the installed Zapret 2 in the Store DB;
4. `%CURRENT%`.

For winws1, the analyzer does not use `BuiltinVersion`. Errors of the resolver or of the DB do not block the import.

### Zapret variables in the ConfigItem

#### Usual `variables`

A used variable without alternatives becomes a line in `variables`:

```text
%LISTS%=$GETCURRENTDIR()/lists/list-general.txt
```

#### `commaVars` and `availableCommaVarsValues`

A variable with alternatives goes to `commaVars`. The commented `set` values and the values from the static walk become the list in `availableCommaVarsValues`.

The startup string keeps the reference `%NAME%`. Thus the user can change the value in the UI widget and does not have to edit the full line.

#### Unknown variables

The analyzer adds each unknown `%NAME%` from the startup string, the main values or the alternatives as:

```json
"commaVars": {
  "NAME": "$EMPTY"
}
```

The `%~...%` forms are not included. For an unknown variable that the startup string or a used variable requires, the analyzer also adds the warning `UNRESOLVED_BATCH_VARIABLE`.

#### Conditional variables and `jparams`

A variable can become a boolean switch if a statically read called script assigns it. If the analyzer sees exactly two different non-empty values for the variable, it makes:

- a boolean parameter `use<VariableName>` in `jparams`;
- a conditional variable in `variables`.

The result has this format:

```text
%GameFilter%=$LOCALCONDITION(useGameFilter==true ? 1024-65535 $SEPARATOR 12)
```

The current value of the variable sets the initial state of the boolean parameter. The analyzer removes all characters except letters, digits and `_` from the parameter name. A name collision gets a number suffix.

If the analyzer sees a different number of values, it does not make a boolean switch.

#### `ACTIVE_*.bin` switches

The analyzer can convert a reference such as `%ROOT%ACTIVE_name.bin` into a separate variable that the user can select.

The analyzer does these steps:

1. It expands the folder from the variable `ROOT`.
2. It finds all `*.bin` files in the folder whose names do not start with `ACTIVE_`. It sorts them by name.
3. It compares the SHA-256 of the active BIN file with each candidate.
4. The last candidate with the same contents becomes the current value.
5. All found BIN files become values in `availableCommaVarsValues`.
6. The reference becomes `%<selector>%`.

The reference stays unchanged if the `ROOT` variable is unknown, if the folder does not exist or if the folder has no candidates. If the active file is missing or no candidate has the same contents, the analyzer still makes the switch. The current value is then the original `ACTIVE_` file path.

### Zapret ConfigItem

The result has these basic fields:

```json
{
  "meta": "pUC:v1.0",
  "name": "name from the metadata or the file",
  "target": ["component-id", "version-or-%CURRENT%"],
  "jparams": {},
  "variables": [],
  "commaVars": {},
  "availableCommaVarsValues": [],
  "startup_string": "normalized arguments"
}
```

The analyzer changes each sequence of whitespace outside double quotes to one space. It replaces `%~dp0` with `$GETCURRENTDIR()/`. It does not change backslashes.

The Zapret analyzer first uses the internal Zapret or Zapret 2 ID of the generation. `ConfigImportService` then replaces it with the ID of the selected Store component. Thus other installed components with the executable `winws` or `winws2` can also use this parser.

## External resources

### General analyzer

The general analyzer looks for references with these extensions:

- `.bin`;
- `.txt`;
- `.lua`;
- `.dat`;
- `.conf`;
- `.cfg`;
- `.json`.

### Zapret analyzer

The Zapret analyzer looks for `.bin`, `.txt` and `.lua`.

A path can be in quotes or not. The analyzer removes the prefixes `@` and `$` before it checks the file. It resolves a relative path relative to the file that contains the reference.

The analyzer adds a file that exists to `ReferencedFiles`. It adds a missing file to `MissingReferencedFiles` and adds the warning `REFERENCED_FILE_NOT_FOUND`. A missing file that the BAT script generates does not go to `MissingReferencedFiles`.

### Standard LUA libraries of the component

For Zapret, the facade can remove a missing `.lua` file from the missing file list if:

1. the path is inside the folder structure of the imported file;
2. a file with the same relative path exists in the folder of the selected installed component;
3. the config does not require the imported copy through `$GETCURRENTDIR()/relative/path.lua`.

In this case, the facade assumes that the component supplies the LUA file. It removes the related warning.

## Missing files

The wizard requires an explicit decision for each path in `MissingReferencedFiles` before the save.

Two types of decision are possible:

1. a replacement file that exists;
2. an empty file: `ReplacementPath == null`.

`ApplyMissingFileResolutions` accepts only decisions for files that are in the original missing file list. It normalizes the paths with `Path.GetFullPath`. If the selected replacement file does not exist, it adds the blocking error `MISSING_FILE_REPLACEMENT_NOT_FOUND`.

### Auto-corrector

`ConfigImportAutoCorrector` looks for a replacement in two steps:

1. It looks for a file with the same name in the folder of the imported config. The search goes down a maximum of 3 subfolder levels. It skips folders that it cannot open and reparse points.
2. It calls the existing `Autocorrector.FindAutoCorrectPath` with a temporary `packId` that points to the source folder.

The auto-corrector does not copy or change files. It only returns a path that exists. It ignores search exceptions, so that a failed suggestion does not block the wizard.

For a file whose name contains `user` or `debug` (any case), the wizard suggests an empty file and does not search for a replacement.

On the correction page, the user can:

- accept the found replacement;
- select a different file;
- select an empty file;
- accept all available suggestions with the continue button.

## Retarget

A component selection on the result card does not change the original object immediately. Before the test and the save, the wizard does these steps:

1. It applies the decisions for missing files.
2. It calls `ConfigImportService.Retarget`.
3. It makes sure that the config is still successful after the retarget.

### Same executable

If the old and the new target use the same executable, the retarget keeps the version from `config.target`, if it is not empty. It replaces the component ID with the selected ID.

### `winws1 -> winws2`

This is the only conversion between generations in `Retarget`.

1. The retarget writes the source folder as `packId` into a clone of the config.
2. `ConfigurationService.GetStartupParametersByConfigItem` expands `variables`, `commaVars`, `jparams` and the service functions into a ready winws1 command line.
3. The existing `Zapret1ToZapret2Converter` gets the ready line.
4. The converter issues go to `Issues`.
5. If the conversion is successful, the converted line goes to `startup_string`.
6. The retarget clears `jparams`, `variables`, `commaVars` and `availableCommaVarsValues`, because the line is already expanded.
7. The target becomes the selected winws2 component and its version.
8. The retarget clears the temporary `packId`.

If the converter returns an error, `Config` becomes `null`, and the test and the save are blocked.

`Zapret1ToZapret2Converter` is not a part of this add-on. The add-on only calls it and passes its issues without changes.

### Other executable combinations

For other combinations, no special conversion occurs. The retarget replaces the target and the version and keeps the startup string. The UI shows a warning if the detected executable is different from the selected one. This warning is for information and does not block the operation.

The add-on cannot convert `winws2 -> winws1`.

## Config test

`MainPage` does the test run in these steps:

1. It stops the earlier active test.
2. It applies the decisions for missing files.
3. It runs the retarget.
4. It sets `packId` to the folder of the source file temporarily.
5. `ConfigurationService.GetStartupParametersByConfigItem` makes the ready arguments.
6. `ConfigImportTestPlaceholder.Create` prepares the temporary dependencies.
7. It stops the selected component if it operates.
8. It starts the component through `ComponentTasksManager.CreateAndRunNewTask(componentId, arguments)`.
9. It subscribes to the state changes of the task.
10. When the process stops, it deletes the temporary files.

Only one test can operate at a time. A second click on the button of the same card stops the test. When the page closes or unloads, it also stops the process and releases the temporary files.

If the process stops immediately after the start, the test fails.

### Temporary test dependencies

The temporary folder has this pattern:

```text
%TEMP%\CDPIUI\ConfigImportPlaceholders\<GUID>\
```

If at least one missing value must be empty, the class makes one common empty file:

```text
empty.bin
```

If the user selected an existing replacement, the test uses its original path. The class writes generated files as UTF-8 without BOM into the subfolder `generated`, with names such as `0000_<original-name>`.

The class replaces these forms in the ready arguments:

- the expected absolute path;
- the form `$GETCURRENTDIR()/relative/path`.

Mixed `/` and `\` are valid in these forms. The class does not process the forms `%~dp0relative`, `./relative` and a plain `relative/path` if `ConfigurationService` did not expand them earlier.

`Dispose` deletes only a folder in the permitted root `ConfigImportPlaceholders`. The cleanup is best effort.

## Config and resource save

`ConfigImportInstaller.InstallAsync` accepts only a successful result, a name that is not empty and an explicit decision for each missing file.

For a `.cdpiconfig` result (`SharedPackage` is set), the installer calls `ConfigShareService.InstallAsync` with the edited config. The rest of this section applies to the other formats.

### Save root

The installer saves the config in the local service kit:

```text
Store\Items\LocalUserStorage\
```

`ConfigurationService.GetItemFolderFromPackId(SharedConstants.LocalUserItemsId)` gives the exact absolute path.

The installer saves the resources by type:

```text
LocalUserStorage\List\Converted\<importId>\...
LocalUserStorage\Bin\Converted\<importId>\...
LocalUserStorage\Lua\Converted\<importId>\...
```

The extension sets the category:

- `.txt` -> `List`;
- `.lua` -> `Lua`;
- all other resources -> `Bin`.

`importId` is the UTC time `yyyyMMddHHmmss`, the character `_` and a GUID part. The installer cuts the string to 24 characters.

### Relative paths

The installer keeps the original subfolders of a resource. It removes one standard top folder, so that the category does not repeat:

- for `List`: `list` or `lists`;
- for `Lua`: `lua`;
- for `Bin`: `bin` or `fake`.

Example:

```text
original path: lists\ipset-exclude.txt
saved path: List\Converted\<importId>\ipset-exclude.txt
```

The installer does not copy an existing resource outside the folder of the source config. The config keeps the original path of this resource, and the result has the warning `REFERENCED_FILE_OUTSIDE_SOURCE`. The user sees the path in the review and can change it later in the preset. Thus a crafted BAT file cannot add a private file of the user to a preset that the user shares later.

If a missing resource outside the folder of the source config has a replacement, the installer uses only the file name. This does not permit a write outside the target folder: the installer checks the final destination again.

Two saved relative paths can be the same. In this case, the installer adds a suffix to the name of the second file: the first eight hex characters of the SHA-256 of its expected absolute path.

### Resource sources

The installer makes a resource in one of three ways:

1. It copies an existing `ReferencedFile` in the folder of the source config and does not overwrite.
2. It writes a `GeneratedFile` as UTF-8 without BOM.
3. For a missing file with the decision `null`, it makes an empty file. For a missing file with a replacement, it copies the selected file.

### Path changes

Before the save, the installer makes a deep clone of the config through JSON. It changes the paths in:

- `startup_string`;
- each item of `variables`;
- each value of `commaVars`;
- each value in `availableCommaVarsValues`.

The target format is:

```text
$GETCURRENTDIR()/List/Converted/<importId>/file.txt
$GETCURRENTDIR()/Bin/Converted/<importId>/file.bin
$GETCURRENTDIR()/Lua/Converted/<importId>/file.lua
```

The installer knows these forms:

- the absolute original path;
- `$GETCURRENTDIR()/source/path`;
- `%~dp0source/path` for folder mappings;
- the relative `source/path`;
- `./source/path` and `.\source\path`;
- an optional prefix `@` for a single file reference;
- mixed separators `/` and `\`.

The installer first makes the folder mappings. A mapping applies only when one source folder maps to exactly one saved folder. Then the installer changes the paths of the single files.

### Transactional save

For each category, the installer first makes a staging folder:

```text
<Category>\Converted\.<importId>.tmp
```

The installer does these steps:

1. It makes the staging folders.
2. It writes or copies all resources.
3. It prepares the config clone and changes the paths.
4. It moves the staging folders to `<importId>`.
5. It saves the JSON through `ConfigurationService.SaveConfigItem`.
6. If an error occurs, it deletes the folders that it made and the JSON, if it exists.

The deletion is limited to the checked `Converted` root of the category. The rollback is best effort. The installer never deletes the original files.

### Config file name

The installer writes the display name to `name` and `not_converted_name`.

The JSON name has these rules:

1. Unicode letters, digits, `_`, `.` and `-` stay.
2. Each other character, also each whitespace character, becomes `_`.
3. The installer removes `_` and `.` at the start and at the end.
4. An empty result becomes `Imported_Config`.
5. A name collision gets `_2`, `_3` and so on.

The installer saves the config with `packId = LocalUserStorage` through `SaveConfigItem`. The JSON does not have to contain `packId`.

After a successful save, the UI calls `ReInitConfigs()` for the selected component.

## Import wizard

### Step 1. Selection

The file dialog permits a selection of several `.txt`, `.bat`, `.cmd`, `.json` and `.cdpiconfig` files. The user can also drop files on the component settings page (`ViewComponentSettingsPage`). The page then opens the import window with these files and the component ID. A single dropped `.cdpiconfig` file can go to the ConfigShare dialog instead. The default config name is the file name without the extension. For a `.cdpiconfig` package, the default name is the name from the manifest.

### Step 2. Analysis

A background task processes the files one by one. For each file, the wizard does these steps:

1. It finds the applicable target.
2. It runs the import.
3. It makes a separate result card.
4. It shows the success, the warning or the error.

For a `.cdpiconfig` package with a missing component, the wizard offers the Store installer of the component.

The **More** flyout shows the issues in the format `[CODE] [line] message`.

### Step 3. Missing files

The wizard shows this step only if successful results have missing resources. The user can continue when the user resolved each file, or when each remaining file has a suggestion.

### Step 4. Results

Each card lets the user:

- change the name;
- select an installed component;
- see a warning about a different executable;
- see the issues;
- start and stop the test;
- save the card.

The **Save all** button saves all cards with `CanSave == true` one by one. The wizard removes the saved cards from the list. Cards with an error stay in the list.

## Issues

Each issue has:

- `Severity`: `Warning` or `Error`;
- a stable string `Code`;
- a technical `Message`;
- an optional `SourcePath`;
- an optional `LineNumber`.

### General codes

| Code | Level | Condition |
|---|---|---|
| `FILE_NOT_FOUND` | Error | The source file does not exist |
| `TARGET_REQUIRED` | Error | No component ID is selected |
| `EXECUTABLE_REQUIRED` | Error | The target record has no executable |
| `UNSUPPORTED_FORMAT` | Error | The selected analyzer does not support the extension |
| `IMPORT_FAILED` | Error | An unhandled exception occurred during the analysis |
| `EXECUTABLE_LAUNCH_NOT_FOUND` | Error | The general BAT or CMD file has no launch of the selected executable |
| `MULTIPLE_EXECUTABLE_LAUNCHES` | Error | The file has more than one launch of the selected executable |
| `TEXT_CONFIG_EMPTY` | Error | The general TXT file has no arguments |
| `JSON_CONFIG_INVALID` | Error | The JSON is not a supported `ConfigItem` or `OldConfigItem` |
| `EXECUTABLE_MISMATCH` | Error | The Zapret analysis found a generation that is different from the target |
| `UNRESOLVED_VARIABLE` | Warning | The general analyzer cannot resolve a BAT variable |
| `REFERENCED_FILE_NOT_FOUND` | Warning | An external resource is missing |
| `REFERENCED_FILE_OUTSIDE_SOURCE` | Warning | The config uses a resource outside the folder of the imported file. The import does not copy it, and the preset keeps its original path |
| `INCLUDED_FILE_OUTSIDE_SOURCE` | Warning | The config reads an included config file or a called script outside the folder of the imported file |
| `INVALID_RESOURCE_PATH` | Warning | The analyzer cannot normalize a resource path |
| `MISSING_FILE_REPLACEMENT_NOT_FOUND` | Error | The replacement that the user selected does not exist |

For a `.cdpiconfig` file, the issue code is the code of the `ConfigShareException`, for example `SHARE_IMPORT_FAILED`.

### Zapret analyzer codes

| Code | Level | Condition |
|---|---|---|
| `BATCH_READ_FAILED` | Error | The BAT or CMD file cannot be read |
| `TEXT_CONFIG_READ_FAILED` | Error | The TXT file cannot be read |
| `WINWS_LAUNCH_NOT_FOUND` | Error | The BAT or CMD file has no supported winws or winws2 launch |
| `MULTIPLE_WINWS_LAUNCHES` | Error | The file has more than one winws or winws2 launch |
| `TEXT_CONFIG_HAS_NO_OPTIONS` | Error | The TXT file has no `--...` options and no response file |
| `CONFIG_GENERATION_UNKNOWN` | Error | The TXT generation is unknown, and the default is disabled |
| `GENERATION_MISMATCH` | Error | The executable and the options belong to different generations |
| `INCLUDED_CONFIG_NOT_FOUND` | Error | The referenced response file does not exist |
| `INCLUDE_CYCLE` | Error | The TXT includes make a cycle |
| `INCLUDE_DEPTH_EXCEEDED` | Error | The include depth is more than the limit |
| `CONFIG_INCLUDE_IGNORES_OTHER_ARGUMENTS` | Warning | The analyzer ignored arguments near the response file |
| `CALLED_SCRIPT_READ_FAILED` | Warning | A called BAT or CMD file exists but cannot be read |
| `UNRESOLVED_BATCH_VARIABLE` | Warning | A used BAT variable cannot be resolved |
| `GENERATED_FILE_OUTSIDE_SOURCE` | Warning | A generated file is outside the source folder |
| `REFERENCED_FILE_NOT_FOUND` | Warning | A BIN, TXT or LUA file is missing |
| `REFERENCED_FILE_OUTSIDE_SOURCE` | Warning | The config uses a resource outside the folder of the imported file. The import does not copy it, and the preset keeps its original path |
| `INCLUDED_FILE_OUTSIDE_SOURCE` | Warning | The config reads an included config file or a called script outside the folder of the imported file |

### Installer codes

| Code | Condition |
|---|---|
| `IMPORT_RESULT_INVALID` | The analysis or the retarget is not successful |
| `IMPORT_NAME_REQUIRED` | The config name is empty |
| `IMPORT_MISSING_FILE_RESOLUTION_REQUIRED` | A missing file has no decision |
| `IMPORT_INSTALL_FAILED: ...` | An exception occurred during a write, a copy, a path change or the save |
| `<ConfigShare code>: ...` | `ConfigShareService.InstallAsync` failed for a `.cdpiconfig` package |

`ConfigurationService.SaveConfigItem` can return its own error code. The installer passes it to the caller without changes. The installer also adds the codes of `Zapret1ToZapret2Converter` to `Issues` without changes. The converter implementation sets the full list of these codes.

## Example of a direct call

```csharp
using CDPIUI.AddOns.ConfigImport;

var service = new ConfigImportService();
var target = new ConfigImportTarget(
    ComponentId: "CSZTBN062",
    DisplayName: "Zapret 2",
    Executable: "winws2",
    Version: "2.11",
    Directory: @"C:\path\to\zapret2");

ConfigImportResult analyzed = service.Import(
    @"C:\configs\service.bat",
    target);

if (!analyzed.IsSuccessful)
{
    foreach (ConfigImportIssue issue in analyzed.Issues)
        Console.WriteLine($"{issue.Severity}: [{issue.Code}] {issue.Message}");
    return;
}

var resolutions = analyzed.MissingReferencedFiles.Select(path =>
    new ConfigImportMissingFileResolution(path, ReplacementPath: null));

ConfigImportResult resolved = service.ApplyMissingFileResolutions(analyzed, resolutions);
ConfigImportResult retargeted = service.Retarget(resolved, target);

var installer = new ConfigImportInstaller();
ConfigImportInstallResult installed = await installer.InstallAsync(
    retargeted,
    "Imported Zapret Config");
```

CAUTION: Ask the user to confirm `ReplacementPath: null` before you use it for many files. The analyzer does not treat a missing file as resolved automatically.

## Example of a BAT variable conversion

The source fragment:

```bat
set "LISTS=%~dp0lists\list-general.txt"
rem set "LISTS=%~dp0lists\list-alt.txt"
winws2.exe --hostlist=%LISTS% --lua-desync=fake
```

The logical part of the result before the save:

```json
{
  "commaVars": {
    "LISTS": "$GETCURRENTDIR()/lists\\list-general.txt"
  },
  "availableCommaVarsValues": [
    {
      "VarName": "LISTS",
      "CurrentValueIndex": 0,
      "Values": [
        "$GETCURRENTDIR()/lists\\list-general.txt",
        "$GETCURRENTDIR()/lists\\list-alt.txt"
      ]
    }
  ],
  "startup_string": "--hostlist=%LISTS% --lua-desync=fake"
}
```

After the save, the paths of the two values point to `List/Converted/<importId>/...`.

## Limits of the static analysis

The add-on is not a `cmd.exe` interpreter. It processes these constructs partially or not at all:

- `if` branches and `for` loops;
- `set /a` calculations and complex substitutions `%VAR:...%`;
- variable and command names that a script makes at run time;
- `goto` and the general control flow of BAT files;
- commands that make files in a way other than the supported `echo >` and `echo >>` in `load_user_lists`;
- an executable that a wrapper, a function or an unknown construct starts;
- several possible launches with a selection at run time. The add-on treats them as ambiguous on purpose;
- response files in a non-standard format;
- resources with an extension that is not in the known list.

Also note these limits:

- Values that depend on the current OS environment can give different results on different computers.
- The add-on does not check the compatibility of a random component, except for the special Zapret checks.
- The add-on cannot convert winws2 to winws1.
- The test does not isolate the component process and does not repeat the full source BAT.
- The temporary path replacement of the test knows fewer relative forms than the installer.

An unknown variable usually stays: it becomes an editable `commaVar` with the value `$EMPTY`. Thus the user can complete the config manually.

## Extension rules

### A new general component

If its BAT or CMD file starts the executable directly with a usual command line, no separate parser is necessary. Do these steps:

1. Add or install the Store component with correct `Id`, `Directory`, `Executable` and `CurrentVersion` values.
2. If necessary, extend the list of resource extensions.
3. Make sure that the executable detection and the path save operate.

### A special syntax

If the component uses its own include format, conditional variables or generated resources, do these steps:

1. Keep `ConfigImportService` as the facade.
2. Add a separate static analyzer without UI dependencies.
3. Convert the special result to `ConfigImportResult`.
4. Do not run the source script.
5. Return stable issue codes.
6. Describe the generation, the version and the incompatibility rules explicitly.
7. Put the dependencies into `ReferencedFiles`, `MissingReferencedFiles` and `GeneratedFiles`.
8. Make sure that the test and the save change the paths in the same way.
9. Add automated tests with real config sets to `CDPIUI.Tests`.

### A change of the storage structure

Change the structure `List|Bin|Lua/Converted/<importId>` only together with:

- `GetResourceCategory`;
- `GetOrganizedRelativePath`;
- `BuildResources`;
- `RewriteResources`;
- the tests that load the config through `ConfigurationService`;
- the deletion and rollback logic that limits the permitted roots.

## Invariants

Keep these properties in all changes:

1. The analysis of an input BAT or CMD file does not run it.
2. The add-on does not change or delete the source files.
3. A missing file is not resolved without an explicit `ConfigImportMissingFileResolution`.
4. Each import gets a separate resource folder.
5. After the save, the config paths point to the actual category `List`, `Bin` or `Lua`.
6. A write cannot go outside the staging or destination folder.
7. A failed config save starts a rollback of the created resources.
8. `winws1 -> winws2` converts the ready expanded line. The Zapret parser does not mix generations.
9. A native winws2 config is saved without a syntax conversion.
10. The test removes the temporary files after the process stops or the page closes.

## Tests

`CDPIUI.Tests/AddOns` has tests for this add-on:

- `ConfigImportServiceTests.cs` checks `ConfigImportService.TryParseSet`, the warning `REFERENCED_FILE_OUTSIDE_SOURCE` and that the installer does not copy a resource outside the source folder;
- `ZapretConfigImportServiceTests.cs` checks `TrimShellTail`, `ExpandFileModifiers` and the warning `REFERENCED_FILE_OUTSIDE_SOURCE`;
- `ConfigImportGoldenTests.cs` imports Flowseal BAT files from `TestData/ConfigImport` with the two analyzers. It compares the results with the expected files next to the BAT files.

`CDPIUI.Tests/Core/BatchFileEncodingTests.cs` checks the encoding detection. `CDPIUI.Tests/Core/Zapret1ToZapret2ConverterTests.cs` checks the conversion that `Retarget` uses.

A minimum set of manual checks for a change includes:

1. a simple BAT file for a general component;
2. a BAT file with caret continuations;
3. a BAT file with one launch and with several launches;
4. a winws1 BAT file and a retarget to winws2;
5. a native winws2 BAT file;
6. a TXT file with `# BuiltinVersion`;
7. a BAT file that refers only to a TXT response file;
8. `service.bat` with variables, a dispatch and `load_user_lists`;
9. `ACTIVE_*.bin` with the same SHA-256;
10. a known, a missing, a replaced and an empty resource of each type;
11. the paths `list/lists`, `bin/fake`, `lua`, absolute paths and `$GETCURRENTDIR()`;
12. a test run with missing and generated files;
13. a save of two configs with the same name;
14. an artificial `SaveConfigItem` error to check the rollback;
15. a current and an old JSON format.

A successful build proves only the compilation. Also check the actual process arguments, the saved JSON, the resource locations and a start of the config from the usual component page.
