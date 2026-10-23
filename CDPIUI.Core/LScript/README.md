# LScript reference

LScript is a small expression language. Store metadata, preset variables and preset startup strings use it.
The implementation is in `CDPIUI.Core/LScript`:

| File | Content |
|---|---|
| `LScriptCore.cs` | Value functions (`ExecuteScript`, `ExecuteScriptUnsafe`, `LocalCondition`) and install actions (`RunScript`). |
| `DefaultMessageHandler.cs` | Path and file helpers for `$STATICIMAGE`, `$DYNAMICIMAGE` and `$LOADDYNAMIC`. |
| `LScriptMsiHandler.cs` | The install action `install_msi`. |

The GUI adds one function in `CDPIUI/Helper/LScript/LScriptLangHelper.cs` (see "GUI extension").

LScript has two parts:

- Value functions. They replace a `$FUNCTION(argument)` text with a value.
- Install actions. They run commands after the Store downloads an item.

## Value functions

| Function | Result |
|---|---|
| `$STATICIMAGE(path)` | `ms-appx:///Assets/<path>`, an image in the GUI package. |
| `$DYNAMICIMAGE(path)` | `<base>\<path>`. `<base>` is the script argument when it is given, otherwise `Store\Cache\Repo`. The function does not read the file. |
| `$LOADDYNAMIC(path)` | The text of the file `Store\Cache\Repo\<path>`. |
| `$GETCURRENTDIR()` | The folder of the calling item, `Store\Items\<callItemId>`. Without `callItemId`, the folder `Store\Items`. |
| `$GETSRDIR` | The Store repository cache folder, `Store\Cache\Repo`. Only `ExecuteScriptUnsafe` knows this function. |
| `$LOCALCONDITION(condition)` | One of two values. The value of a boolean in `jparams` selects it. See "Conditions". |
| `$EMPTY` | An empty string. The library removes it before it evaluates the other functions. |

`Store\...` folders are in the data directory. See `CDPIUI.Core/README.md`, section "Data directories".

### Placeholders in arguments

If the caller gives `scriptArgs`, each `{...}` text in the argument is replaced with `scriptArgs`.
The GUI uses this to select a language file. Examples:

```text
$LOADDYNAMIC(items/zapret/description_{lang}.md)   scriptArgs = "EN"  ->  text of Store\Cache\Repo\items\zapret\description_EN.md
$DYNAMICIMAGE(icon.png)                            scriptArgs = "C:\Temp\ap123"  ->  C:\Temp\ap123\icon.png
```

The file names in these examples are examples only. `LocalItemsInstallerHelper` uses the second form.
It gives the temporary pack folder as `scriptArgs` and reads the icon of a local pack.
When the Store registers a local pack, `StoreHelper.GetIconPath` keeps the icon as a full path in the item folder.
It does this only when the file exists in the item folder. Otherwise it keeps the `$DYNAMICIMAGE(...)` text.

## Evaluation modes

The library has two evaluation methods. They do not give the same results.

| Property | `ExecuteScript` | `ExecuteScriptUnsafe` |
|---|---|---|
| Where the function can be | Only at the start of the string. The full value must start with `$`. | Anywhere in the string. All occurrences are replaced. |
| Number of functions | One. Text after the call is ignored, except for `$GETCURRENTDIR()`. | All matches. |
| Function names | Case-sensitive. | Not case-sensitive. |
| `$GETSRDIR` | Not supported. | Supported. |
| `$GETCURRENTDIR()` | The item folder plus all text after `$GETCURRENTDIR()`. | Only the item folder. The text after it stays. |
| `$LOCALCONDITION` result | The selected value, not expanded. | The selected value, expanded again with the same arguments. |
| Error | The input string is returned (without `$EMPTY`). | The text of the failed match stays. |
| Path quotes | No. | Optional (`quotePaths`). |

A string that does not start with `$` goes through `ExecuteScript` unchanged, but without `$EMPTY`.
An unknown `$NAME(...)` also stays unchanged.

### Where each mode is used

| Caller | Mode | Arguments |
|---|---|---|
| Preset variable values (`ConfigurationService.GetReadyToUseVariables`) | `ExecuteScript` | `callItemId` = pack id, `jparams` of the preset |
| Preset startup string after variable replacement (`GetStartupParametersByConfigItem`) | `ExecuteScriptUnsafe` | `callItemId` = pack id, `quotePaths: true` |
| File search for presets (`Autocorrector`) | `ExecuteScriptUnsafe` | `callItemId` = pack id |
| Store icons and texts in the GUI | `ExecuteScript` through `LScriptLangHelper` | the language code as `scriptArgs` for texts |

### Sequence for a startup string

The library expands preset variables before it expands LScript in the startup string:

1. Each `%NAME%=value` gives `NAME` = `ExecuteScript(value)`. A value that starts with `$GETCURRENTDIR` or `$GETSRDIR` (not case-sensitive, after white space) stays unchanged.
2. The library replaces `%NAME%` in the startup string (16 passes maximum).
3. The library replaces `commaVars`.
4. `ExecuteScriptUnsafe` expands the remaining functions, for example `$GETCURRENTDIR()`.

Step 1 evaluates the other values that start with `$`. A value that does not start with `$` stays as text.
Step 4 then expands the remaining functions, with path quotes. Thus a folder path with spaces gets quotes also when a variable value starts with `$GETCURRENTDIR()`.

Example of a preset in the pack `MyPack`:

```json
{
  "target": ["CSZTBN012"],
  "jparams": { "useGameFilter": true },
  "variables": [
    "%GAMEFILTER%=$LOCALCONDITION(useGameFilter==true ? 1024-65535 $SEPARATOR 12)",
    "%LIST%=--hostlist=$GETCURRENTDIR()/lists/list-general.txt"
  ],
  "startup_string": "--wf-udp=443,%GAMEFILTER% %LIST%"
}
```

Result, when the data directory has no spaces:
`--wf-udp=443,1024-65535 --hostlist=<DataDirectory>\Store\Items\MyPack/lists/list-general.txt`.

## Path quotes

`ExecuteScriptUnsafe` with `quotePaths: true` adds quotes to the result of `$GETCURRENTDIR()` and `$GETSRDIR`. These conditions apply:

- The folder path contains a white-space character, for example `C:\Users\Ivan Petrov\...`.
- The number of `"` characters before the function is even. Thus the function is not already in quotes.

Only the folder is in quotes. The rest of the argument stays after the closing quote:

```text
--hostlist=$GETCURRENTDIR()/lists/a.txt     ->  --hostlist="C:\Users\Ivan Petrov\CDPIUI\Store\Items\MyPack"/lists/a.txt
--hostlist="$GETCURRENTDIR()/lists/a.txt"   ->  --hostlist="C:\Users\Ivan Petrov\CDPIUI\Store\Items\MyPack/lists/a.txt"
```

The components accept a quote in the middle of an argument (see the comment on `QuoteIfNeeded`).
`ExecuteScript` never adds quotes.

## Conditions

Syntax:

```text
$LOCALCONDITION(<name>==<true|false> ? <value if equal> $SEPARATOR <value if not equal>)
$LOCALCONDITION(<name>==<true|false> ? <value if equal> : <value if not equal>)
```

Rules (`LScriptCore.LocalCondition`):

- `<name>` is a key of `jparams`. The condition is true when `jparams[name]` equals the literal.
- The literal must parse as a .NET `bool` (`true`, `false`, not case-sensitive).
- The first `?` ends the condition. The condition cannot contain `?`.
- When the text contains `$SEPARATOR`, the first `$SEPARATOR` separates the two values.
- Without `$SEPARATOR`, the first `:` after `?` separates the values. A drive colon (a letter, then `:`, then `\` or `/`) is not a separator.
- The library removes white space at the start and end of each value. It removes `$EMPTY` from the selected value.

Results for problems:

| Problem | Result |
|---|---|
| No `?`, no separator, or the separator is before `?` | An empty string and a warning in the log. |
| No `==`, or more than one `==` | An empty string and a warning. |
| `<name>` is not in `jparams` | An empty string and a warning. |
| The literal is not `true` or `false` | An exception. The caller returns the original text. |
| `jparams` is `null` | An empty string and a warning. |

Use `$SEPARATOR` when a value contains `:`. The ConfigMaker and the ConfigImport add-on always write `$SEPARATOR`.

Examples from the code and tests:

```text
$LOCALCONDITION(useGameFilter==true ? 1024-65535 $SEPARATOR 12)
useA==true?C:\lists\a.txt:D:\lists\b.txt      (test of LocalCondition: drive colons are skipped)
$LOCALCONDITION(useFake==false ? $EMPTY $SEPARATOR --dpi-desync=fake)
```

The test is `CDPIUI.Tests/Core/LScriptCoreTests.cs`. The third line is an example only.

### Condition helpers

- `CreateCondition(name, on, off)` returns `"<name>==true ? <on> : <off>"`. It does not add `$LOCALCONDITION(...)`.
- `GetNameOnOffValuesFromConditionString(expression)` reads `%VAR%=$LOCALCONDITION(param==true ? on $SEPARATOR off)`.
  It returns the tuple (`param`, `VAR`, `on`, `off`). The editors use it to show switches.
  Without `$SEPARATOR`, it uses the first `:` after `?`. It skips drive colons, as `LocalCondition` does.

## Install actions

The Store runs install actions with `LScriptCore.RunScript`. They come from two item fields:

- `before_install_actions`: run after the download and before the database registration.
- `after_install_actions`: run after the database registration.

The Store gives the item folder as `CurrentDirectory` to the two lists.

Only Store items and signed local packs (`.cdpisignedpack`) have install actions.
The library ignores the actions of a pack that is not signed.

### Syntax

```text
$<NAME>(<command>, <argument 1>, <argument 2>);$<NAME>(<command>, ...)
```

- `;` separates the actions. An empty action (for example after the last `;`) is skipped.
- The text inside the first pair of parentheses holds the command and the arguments.
- `, ` (a comma and one space) separates the command and the arguments. A comma without a space does not separate them.
- The name between `$` and `(` is not checked. Only the command name is important.
- An unknown command gives a warning in the log. The next action then runs.
- A cancellation stops the sequence before the next action.

### Commands

| Command | Arguments | Effect |
|---|---|---|
| `finish_component_setup` | component name | Calls `ComponentTasksManager.UpdateTaskList`. For `byedpi`, `spoofdpi` or `nodpi`, it sends the `ProxySetupRequired` notification when `PROXY/proxyType` is `None`. |
| `install_msi` | path, `true`/`false` | Installs an MSI through the tray. The second argument tells if the library deletes the MSI after the installation. |
| `download_easy_designer_annotation_file` | HTTPS URL | Adds `DOWNLOAD=<url>$SEPARATORedannotationfile;` to the result of `RunScript`. |
| `apply_config` | any | No operation. Older pack builders write it. The preset lists load again after each installation. |

The names in the first column are case-sensitive and lower case.
The component names of `finish_component_setup` are also case-sensitive.

#### `install_msi`

- The path is relative to the item folder (`CurrentDirectory`).
- The full path must stay inside the item folder. Otherwise the action throws `MsiInstallException`.
- A path that ends with `$ALL` installs each `*.msi` file in that folder. Subfolders are not included.
- The second argument must parse as a `bool`. Otherwise the action throws `MsiInstallException`.
- A failed installation throws an exception. Then the item installation fails.
- An installation can take a maximum of 30 minutes (`MsiInstallerService`).
- If the MSI requests a system restart, `CoreEvents.RestartRequired` gets the name of the item folder.

```text
$RUN(install_msi, drivers/WinDivert.msi, true)
$RUN(install_msi, redist/$ALL, false);$RUN(finish_component_setup, byedpi)
```

`RUN` and the file names are examples only. Any name is accepted between `$` and `(`.

#### `download_easy_designer_annotation_file`

The command does not download the file. It returns a `DOWNLOAD=` entry.
`OnlineItemsInstallationService.LaunchBeforeInstallActions` reads the result of the two action lists:

- The URL must be an absolute HTTPS URL. Otherwise the installation fails.
- The file goes into the item folder with the name `edannotationfile` and the extension of the URL file.
- A URL that ends with `.zip` is extracted into the item folder.

If a download from `before_install_actions` fails, the item installation fails.
If a download from `after_install_actions` fails, the library writes a warning to the log. The item stays installed.

## GUI extension

`LScriptLangHelper.ExecuteScript` (in the `CDPIUI` project) adds one function:

| Function | Result |
|---|---|
| `$Q_LINK(key)` | The localized GUI string `<scriptArgs><key>` from the WinUI3Localizer resources. |

All other strings go to `LScriptCore.ExecuteScriptWork`.

The GUI also uses `GetScriptArgs` and `GetArgumentsFromScript` to read the argument of a `$NAME(...)` text, for example a URL.

## Preset name keys

Preset names are not LScript. `ConfigurationService` replaces each `$WORD(key)` in the `name` of a preset.
The replacement is the value of `key` in the localization file of the pack (`localized_strings_directory` in `init.json`).
When a preset is saved, the file keeps the original name with the keys.

## Limits

- The parentheses in an argument must be balanced. An argument can contain a call, for example `$LOCALCONDITION(useA==true ? $GETCURRENTDIR()/a.txt $SEPARATOR b.txt)`.
- Only `ExecuteScriptUnsafe` expands more than one function in a string. It also expands the functions in the selected value of `$LOCALCONDITION`.
  `ExecuteScript` returns the selected value without expansion. In a startup string, step 4 then expands it.
- A condition can test only one boolean with `==`. It has no `!=`, `&&` or `||`.
- `$LOADDYNAMIC` reads only from the Store repository cache. It does not use `scriptArgs` as the base folder.
- An action argument cannot contain `, ` or `;`, because these characters are separators.
