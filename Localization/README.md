# Application translations

Edit the UTF-8 JSON files here. They are the source of truth for application copy in English (`en-us`) and Russian (`ru`). Each catalog is a flat object with stable, searchable resource IDs:

```json
{
  "SelectAppContentDialog.Title": "Add application",
  "PT_ReportTitle": "Config test results for component {0} ({1})"
}
```

`Resources.json` contains the main UI copy. The other UI catalogs group help, settings, troubleshooting, widgets, and the welcome wizard. `Tray.json`, `Update.json`, and `EditHostFile.json` contain notifications, updater text, and hosts editor text respectively.

Keep the same keys in both languages. Preserve formatting arguments such as `{0}` and `{1}`, including their format specifiers. Use `\n` for line breaks. Do not build sentences by joining translated fragments: put the complete sentence and its arguments in the catalog. JSON strings contain ordinary text, including quotes and XML characters; the build escapes them when generating RESW.

Two inherited translator notes: `AboutButton.Content` is the About menu/header text; `ORSeparatorTextBlock.Text` should keep its surrounding `--` and translate the word in between.

## How the application reads translations

- **WinUI:** `build/Localization.targets` calls `build/Generate-Localization.ps1` with Windows PowerShell during the GUI build. It generates RESW files under `CDPIUI/obj/localization/<platform>/<configuration>` and copies them to the existing `Strings/<language>` output/publish layout. Do not edit or commit generated RESW files. This preserves WinUI3Localizer's XAML `l:Uids.Uid` bindings and language switching. C# continues to use `GetLocalizedString("ResourceId")`; XAML property keys have the form `Uid.Property`.
- **Tray and emergency UI:** `CDPIUI.Shared.Localization.JsonLocalizer` reads embedded JSON resources, caches catalogs, and falls back to English if a selected-language key is absent. A completely unknown key returns its ID for diagnosis.
- **Standalone tools:** the updater and hosts editor link the same lookup code and embed their own JSON catalogs in their executables. They do not load a translation file or another application assembly while elevated or while replacing application files. Their `Strings` classes only expose resource IDs.
- **Language selection:** `LocalePreference` normalizes the saved app language (`SYSTEM/language`), including regional forms such as `ru-RU` and `en-GB`. An invalid/missing preference uses the Windows UI language, then English. App launchers pass `--language <id>` to the tools; a tool started directly without the option uses Windows UI language.

The language picker refreshes its selected and dropdown display names when the app language changes. The existing collection and language items remain in place, so refreshing a caption does not select another language or write another preference. Stored language IDs do not change.

Product names, physical key names, protocol identifiers, file names, command syntax, persisted migration metadata, and diagnostic logs remain invariant. Localized long-form help and license documents remain in their language-specific Markdown folders. User and Store content retain their own data/localization formats.

Exception dialogs read embedded JSON directly through `EmergencyLocalization`, without calling WinUI3Localizer. Missing, empty, malformed, or unreadable selected-language resources fall back to English. If both catalogs fail, a nonempty resource ID remains visible. Preference-read failures fall back to Windows UI language. The critical window uses the system title bar and does not read saved window preferences or create the custom title bar (which has its own regular localization bindings). Process-fatal conditions such as stack exhaustion, out-of-memory, or an unavailable native UI runtime cannot be recovered by a localization service.

The startup readiness barrier exists before initialization begins. A synchronous failure can activate the critical window and reenter the application launch handler; regular window creation still waits for the same pending barrier and receives its failure. Repeated initialization requests do not start a second initializer.

Visible toggle switches localize `OnContent` and `OffContent` through their JSON UID, independently of the Windows language. The compact config-editor switch intentionally hides both labels. Settings breadcrumbs and the selected editor description refresh when the application language changes; the page detaches its language event when unloaded. Copy-control tooltips and accessible names also use catalogs, including in error dialogs where the regular localizer is unavailable.

## Verification

Run `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release`. Localization tests check JSON parsing/duplicate keys, language parity, formatting-argument parity, embedded-resource lookup for every entry, language selection, standalone tool localization, visible XAML literals, and injected failures in JSON/resource/preference reads. The invariant-name allowlist is intentionally short; use a resource for new UI text.

The linked language-item tests check property-change notifications and suppress duplicate notifications for equal captions. Their headless converter-interface contract does not verify WinUI binding. On Windows, also check the selected label and the open dropdown through EN → RU → EN without restart, then verify each language after restarting the installed executable.

Build/publish the GUI normally on Windows; generation is automatic and uses the Windows PowerShell already provided by the OS. The runtime `Strings` paths and RESW names remain compatible with the MSI file list.

To support another language, translate every catalog into a new language folder, extend `LocalePreference` and the language picker, and include that folder in the embedding rules of the shared library/tools and the generated-content rules in `build/Localization.targets`. Also translate the relevant help, license, and Store content.
