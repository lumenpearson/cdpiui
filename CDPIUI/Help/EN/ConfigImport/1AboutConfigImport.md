# Import presets from files

This utility moves presets from other applications or from saved files into CDPI UI. It finds the launch arguments and the related lists and libraries. Then you can test and save the result.

[Open the import utility](cdpiui://Tools/ImportConfig)

### Which files are supported?

- `.bat` and `.cmd` launch scripts of components;
- `.txt` files with launch arguments;
- `.json` presets in a supported CDPI UI format;
- `.cdpiconfig` presets that were shared from CDPI UI with their resources.

You can select more than one file. The wizard makes a separate result card for each file.

A usual JSON document is not a CDPI UI preset. A report from a different utility or a settings file of an unknown application is also not a preset. The import of these files can fail.

### What must I prepare?

- Install the component for which the preset was made. You cannot select or test a component that is not installed.
- Keep the BAT, CMD or TXT file together with its list, BIN and LUA folders. The wizard tries to find and copy these dependencies.
- If possible, keep a backup copy of the original package.

### Is the analysis safe?

During the analysis, CDPI UI only reads the selected files. It does not run the BAT or CMD script or its commands. It does not change or delete the original files.

The test button on the result page is different. It starts the selected installed component with the imported arguments.

WARNING: Use the test button only with files from a source that you trust.

Next: [Select and prepare files](cdpiui://Help/ConfigImport/2ChooseImportFiles/).
