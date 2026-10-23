# Errors and limits

If a card has the **Warnings** or **Failed** status, select the status. The details show a problem code. If possible, they also show the line number in the source file.

### Frequent problems

**The JSON file is not supported**

The utility accepts CDPI UI JSON presets in a supported format. The utility cannot import a JSON document only because it has the `.json` extension. For example, it cannot import a Zapret2 scan report or settings from a different application. Find an exported preset. Or use a BAT, CMD or TXT file with launch arguments.

**The wizard cannot find or select the component**

First, install the necessary component from the CDPI UI Store. The wizard can use a component only if its folder and executable file are on this computer.

**The preset is not applicable to the selected component**

Select the component that the preset author specifies. Two components with a similar purpose do not always support the same arguments.

**A referenced file is missing**

Go back to the original preset archive or folder. Find the necessary list, BIN file or LUA file. Select it manually. Make an empty file only for an optional resource.

**The wizard found no component launch or more than one launch**

The wizard cannot get one clear set of arguments. Do one of these steps:

- Try a different file from the package.
- Make a copy of the BAT or CMD file and remove the unnecessary launch options from the copy.
- Save the necessary arguments in a separate TXT file.

**The test stops immediately, or the saved preset does not operate**

Examine the selected component, the warnings and your decisions for the missing files. Make sure that you imported the working launch option. Some presets have more values that you must set in the component settings.

### For advanced users

The static analysis can examine only known structures. Complex `if`, `for` and `goto` logic can be identified only partially. This also applies to calculated variables, nonstandard include files, wrapper launches and dynamic resource creation. The analysis does not run the BAT and CMD commands. Thus, you possibly must correct the result of such a script manually.

Back to the start: [Import presets from files](cdpiui://Help/ConfigImport/1AboutConfigImport/).

[Start a new import](cdpiui://Tools/ImportConfig)
