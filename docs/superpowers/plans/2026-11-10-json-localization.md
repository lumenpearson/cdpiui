# JSON localization implementation plan

**Goal:** Keep application translations in readable JSON catalogs and resolve visible text in the user's selected language.

**Architecture:** Preserve WinUI3Localizer and its live XAML updates. Generate its RESW input from JSON during the Windows GUI build. Embed JSON in the tray/shared library and in standalone tools so localization does not introduce external DLL or file dependencies for elevated tools.

**Constraints:** Preserve resource IDs, multiline values, formatting placeholders, installer paths, and tool isolation. Keep protocol identifiers, product names, executable arguments, and developer diagnostics invariant. English is the fallback for unsupported languages and missing translations.

- [x] Add catalog integrity tests and observe failure before migration.
- [x] Convert existing RESW catalogs and C# translation tables to `Localization/<language>/<catalog>.json`.
- [x] Generate RESW under `obj` and copy to the existing `Strings` output layout; remove editable RESW copies.
- [x] Add shared JSON lookup and language normalization; use embedded catalogs in the tray and linked source in standalone tools.
- [x] Pass the selected language to tools; remove remaining visible literal labels and translation fallbacks where feasible.
- [x] Document editing and verify catalogs, language selection/fallback, tools, libraries, and the GUI build.

## Acceptance state

Owning task: https://github.com/lumenpearson/cdpiui/issues/206. Implementation is prepared on `dev/json-localization`, based on policy/main revision `86417c94d12f41cbe0879a67540fcb4f81b710c2`. The primary checkout and its WIP remain intact.

The implementation has source review and local build/test evidence. Integration, required CI on the final PR HEAD, and the native EN/RU/emergency-window acceptance slot remain separate, open acceptance items. This plan checklist records implementation steps, not delivery acceptance.

The recovery requirement includes malformed/missing/blank translations, failed resource and preference reads, a broken exception formatter, unavailable optional metadata, and a failed logger. Emergency dialogs use embedded resources directly; regular window creation stays gated by successful initialization. Process-fatal CLR or native runtime failures remain outside recoverable localization behavior.
