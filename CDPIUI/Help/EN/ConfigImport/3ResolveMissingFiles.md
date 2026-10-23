# Missing files

Some presets use separate site lists, BIN files or LUA libraries. If the wizard cannot find a resource at the expected path, it asks you to select a solution. You must resolve each missing file before you can save the preset.

### Available actions

| Action | Result |
| ------ | ------ |
| **Apply autocorrect** | The wizard uses a file that CDPI UI found in a different applicable folder. Examine the recommended path before you apply it. |
| **Choose file** | You select an existing file. Use a resource from the same preset package with the expected extension. |
| **Create empty** | The wizard saves an empty file in place of the missing resource. |

Use an empty file only when the resource is optional. For example, some user lists or log files can be empty at the start.

CAUTION: Do not replace a necessary LUA library or BIN file with an empty file. Do this only if the preset author tells you that the file is optional.

For files with `user` or `debug` in the name, the wizard can recommend an empty file immediately. This is a recommendation. It does not guarantee compatibility.

If each unresolved file has an AutoCorrect recommendation, **Continue** applies these recommendations automatically. If a file has no recommendation, select an action for it first.

When you save the preset, CDPI UI copies the necessary resources into its own storage. The original files stay in their location. CDPI UI does not delete them.

Next: [Review, test and save](cdpiui://Help/ConfigImport/4ReviewTestAndSave/).
