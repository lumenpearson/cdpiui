# Application translations

Edit the UTF-8 RESW files here. They are the source of truth for application copy in English (`en-us`) and Russian (`ru`). Each catalog has stable, searchable resource IDs:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="SelectAppContentDialog.Title" xml:space="preserve">
    <value>Add application</value>
    <comment>The title of the application selection dialog.</comment>
  </data>
  <data name="PT_ReportTitle" xml:space="preserve">
    <value>Config test results for component {0} ({1})</value>
  </data>
</root>
```

`Resources.resw` contains the main UI copy. The other UI catalogs group help, settings, troubleshooting, widgets, and the welcome wizard. `Tray.resw`, `Update.resw`, and `EditHostFile.resw` contain notifications, updater text, and hosts editor text respectively.

Keep the same keys in both languages. Preserve formatting arguments such as `{0}` and `{1}`, including their format specifiers. Use literal line breaks or `&#10;`; use `&#13;&#10;` when an exact CR/LF pair is required. Do not replace a line break with the two literal characters `\n`. Do not build sentences by joining translated fragments: put the complete sentence and its arguments in the catalog. RESW values use XML escaping: `&amp;` for `&` and `&lt;` for `<`. Keep `xml:space="preserve"` and put translator context in a `<comment>` element.

Encode an intentional space at the end of a source line as `&#32;`. This preserves the translated value while keeping branch-wide whitespace checks clean.

Two inherited translator notes: `AboutButton.Content` is the About menu/header text; `ORSeparatorTextBlock.Text` should keep its surrounding `--` and translate the word in between.

## How the application reads translations

- **WinUI:** `CDPIUI/CDPIUI.csproj` directly copies the 20 UI RESW sources to `Strings/<language>/<catalog>.resw` during build and publish. The two main `Resources.resw` inputs also enter the compiled PRI. There is no conversion or generator step. WinUI3Localizer retains its XAML `l:Uids.Uid` bindings and live language switching. C# continues to use `GetLocalizedString("ResourceId")`; XAML property keys have the form `Uid.Property`.
- **Tray and emergency UI:** `CDPIUI.Shared.Localization.ReswLocalizer` reads embedded RESW resources, caches catalogs, and falls back to English if a selected-language key is absent. A completely unknown key returns its ID for diagnosis.
- **Standalone tools:** the updater and hosts editor link the same lookup code and embed their own RESW catalogs in their executables. They do not load a translation file or another application assembly while elevated or while replacing application files. Their `Strings` classes only expose resource IDs.
- **Language selection:** `LocalePreference` normalizes the saved app language (`SYSTEM/language`), including regional forms such as `ru-RU` and `en-GB`. An invalid/missing preference uses the Windows UI language, then English. App launchers pass `--language <id>` to the tools; a tool started directly without the option uses Windows UI language.

The language picker refreshes its selected and dropdown display names when the app language changes. The existing collection and language items remain in place, so refreshing a caption does not select another language or write another preference. Stored language IDs do not change.

Product names, physical key names, protocol identifiers, file names, command syntax, persisted migration metadata, and diagnostic logs remain invariant. Localized long-form help and license documents remain in their language-specific Markdown folders. User and Store content retain their own data/localization formats.

Exception dialogs read embedded RESW directly through `EmergencyLocalization`, without calling WinUI3Localizer. Missing, empty, malformed, or unreadable selected-language resources fall back to English. If both catalogs fail, a nonempty resource ID remains visible. Preference-read failures fall back to Windows UI language. The critical window uses the system title bar and does not read saved window preferences or create the custom title bar (which has its own regular localization bindings). Process-fatal conditions such as stack exhaustion, out-of-memory, or an unavailable native UI runtime cannot be recovered by a localization service.

The startup readiness barrier exists before initialization begins. A synchronous failure can activate the critical window and reenter the application launch handler; regular window creation still waits for the same pending barrier and receives its failure. Repeated initialization requests do not start a second initializer.

Visible toggle switches localize `OnContent` and `OffContent` through their resource UID, independently of the Windows language. The compact config-editor switch intentionally hides both labels. Settings breadcrumbs and the selected editor description refresh when the application language changes; the page detaches its language event when unloaded. Copy-control tooltips and accessible names also use catalogs, including in error dialogs where the regular localizer is unavailable.

## Verification

Run `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release`. Localization tests check RESW/XML parsing, authoring format, project resource declarations, translator context, duplicate keys, language parity, formatting-argument parity, embedded-resource lookup for every entry, language selection, standalone tool localization, visible XAML literals, and injected failures in XML/resource/preference reads. The invariant-name allowlist is intentionally short; use a resource for new UI text.

The project declaration tests guard all GUI Content destinations and output/publish copy settings, both main PRI inputs, all Shared manifest names and each standalone tool's two manifest names. They complement actual build/publish artifact inspection; they do not replace MSBuild or native verification.

The linked language-item tests check property-change notifications and suppress duplicate notifications for equal captions. Their headless converter-interface contract does not verify WinUI binding. On Windows, also check the selected label and the open dropdown through EN → RU → EN without restart, then verify each language after restarting the installed executable.

Build/publish the GUI normally on Windows; resources are copied directly, without a PowerShell generator. The runtime `Strings` paths and RESW names remain compatible with the MSI file list.

To support another language, translate every catalog into a new language folder, extend `LocalePreference` and the language picker, and include that folder in the embedding rules of the shared library/tools and the direct Content/PRI rules in `CDPIUI/CDPIUI.csproj` and the installer inventory. Also translate the relevant help, license, and Store content.


Embedded manifest names use `Localization.<language>.<catalog>.resw`. The shared assembly embeds all 26 catalogs; each standalone tool embeds only its own two catalogs. The embedded reader prohibits DTDs and external resolution. It rejects invalid roots, duplicate names, malformed entries and XML documents above 4,194,304 characters without publishing a partial cache. The reader never calls logging, settings or UI when recovering from a resource failure.

Verify actual build/publish files and values, compiled PRI maps, shared embedded catalogs and each tool's embedded catalogs. Source project declarations alone do not prove resource delivery. Native EN/RU switching and recovery retain their separate exact-revision acceptance.

## JSON beta

`dev/beta-json-localization` preserves the separate frozen JSON authoring/generation feature under [#235](https://github.com/lumenpearson/cdpiui/issues/235). Stable RESW and active-branch adoption belong to [#236](https://github.com/lumenpearson/cdpiui/issues/236). Do not automatically merge the beta into stable development or maintain duplicate editable formats there. Historical evidence retains its original format and revision. Store and user JSON serialization remain unchanged.
