# Select and prepare files

1. [Open the import utility](cdpiui://Tools/ImportConfig).
2. Select **Next**.
3. In the file picker, select one or more preset files.
4. Wait until the analysis is complete.

If you select more than one file, the wizard examines them one at a time. It shows the name of the current file.

### Which format must I select?

- Select `.bat` or `.cmd` when the preset is a ready-to-run script.
- Select `.txt` when the file contains launch arguments of a component.
- Select `.json` only for an exported CDPI UI preset or a compatible preset in the old format.

The main file can have `lists`, `bin`, `fake` or `lua` folders near it. Do not change the package structure until the import is complete. Then the wizard can find the resources.

### What occurs after the analysis?

- If the wizard finds all necessary dependencies, the result page opens.
- If some files are missing, the **Missing files** page opens first.
- If the wizard cannot identify a file, the result card of the file has the **Failed** status. To see the cause, select the status.

### For advanced users

The wizard is not a full Windows command-line interpreter. It finds a known launch of an installed component and gets its arguments. A file can have more than one possible launch, complex conditions, loops or dynamic commands. Then you possibly must make the file simpler. A usual BAT or CMD file must contain one clear launch of a component.

Next: [Resolve missing files](cdpiui://Help/ConfigImport/3ResolveMissingFiles/).
