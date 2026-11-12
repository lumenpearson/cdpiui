# Isolate JSON localization and restore complete RESW authoring

Date: 2026-11-12. Owning issues: #235 (beta preservation), #236 (stable implementation and rollout).

## User intent

Keep JSON localization as one separate beta feature and stop developing it. Main and every active dev branch must use RESW while retaining the quality of the accepted JSON-era localization. Do not mechanically revert the migration. The user explicitly authorized coordination with owners of chats in lumenpearson/cdpiui. Historical snapshots and foreign working copies must remain recoverable.

## Source and preservation boundary

Actual main is fe29acfc4238228da3a6aebb98fff4345c3da106. It includes PR207, PR229 and the accepted decomposition through PR227. Primary local main remains 7ead95f with old uncommitted localization work. It is not the implementation base and must remain untouched until its owner-controlled disposition is agreed. Other occupied worktrees retain their source, index and ignored outputs. No force push, historical rewrite, branch deletion or credential copying.

## Stable architecture (implementation choices)

Keep all 26 translation catalogs under Localization/en-us and Localization/ru, with the same catalog names and resource identifiers, as editable .resw XML. Seed values from CURRENT main JSON, including the later caption/toggle fixes. Preserve all formatting arguments, whitespace and CR/LF. Add the two documented legacy translator notes as RESW comments; retain documentation explaining catalog purpose and invariants. This keeps a single catalog location and changes its format rather than reviving stale content.

WinUI continues using WinUI3Localizer. Replace the generated-content import in CDPIUI/CDPIUI.csproj with direct Content items from the 20 UI RESW inputs, linked/copied/published as Strings/<language>/<catalog>.resw. Keep explicit PRIResource inputs for the two Resources.resw catalogs, as in the current generator. Remove build/Localization.targets and build/Generate-Localization.ps1 from stable source. No generated resource is an authoring source.

Replace JsonCatalog with internal ReswCatalog(Func<string,string,Stream?> openResource), retaining Get(catalog,key,language). Parse XML with XmlReader, DtdProcessing.Prohibit, XmlResolver=null, and a document-size bound chosen from the real largest catalog plus margin. Cache per catalog/language. Read data/name/value strings only, preserve values, reject malformed/duplicate entries, and treat missing/empty/unreadable resources as an empty catalog. Retain the fatal-exception filter. Lookup remains selected language -> English -> resource ID. Never call logging, settings or UI from the fallback reader.

Replace public JsonLocalizer with ReswLocalizer and .json manifest-resource suffixes with .resw. Keep LocalePreference byte-for-byte unless a test identifies an actual defect. Update GUI emergency lookup, tray LocaleHelper and Update/EditHostFile Strings adapters to the RESW loader. Embed all 26 RESW catalogs in CDPIUI.Shared using stable manifest names. Standalone tools continue linking the shared source and embedding ONLY their own two catalogs; they must not depend on a deployment DLL or external resource file during elevation/update.

Keep current localized XAML/C# text replacements, emergency title-bar isolation, startup readiness gates, language propagation via --language, and PR229's in-place caption updates and identity/event behavior. Removing JSON is a format/backend change, not a rollback of these fixes. Stored language IDs, external identifiers, executable arguments, security/installer/decomposition behavior and unrelated resource assets remain unchanged.

Adapt JsonLocalizerTests to ReswLocalizerTests. Adapt LocalizationCatalogTests to secure XML parsing and retain all current checks for key/format parity, embedded lookup of each entry, XAML/style literals and toggle labels. Convert failure fixtures to RESW and add malformed XML, duplicate-name and prohibited-DTD cases. Keep EmergencyRecoveryTests and language-caption regression tests. Add source/project assertions that no application JSON catalog/generator/backend survives and all expected Content/PRI/embedded items resolve. Do not prohibit unrelated Store/preset JSON or rewrite historical evidence mentioning JSON.

## Beta branch

Use dev/beta-json-localization as the sole maintained JSON feature exception. Freeze a current-main-compatible JSON implementation, including all its tests/build/tool/fallback dependencies. The baseline and beta share the same translations and common behavior fixes. After the stable candidate exists, verify their product-path comparison contains only format/backend/generation/associated tests and documentation. Add a short frozen-beta explanation with the exact stable/beta pins and safe checkout instructions. Do not auto-merge the beta into normal development. Preserve old published refs as history with explicit inactive/superseded disposition rather than rewriting them.

## Branch rollout

Before mutation, enumerate actual remote/local refs, PRs, occupied worktrees, exact HEAD/index/dirty state and execution owners. Named owners confirm which dev branches are active and which are frozen accepted evidence. Root/coordinator retains sole UI-queue ownership. Local primary WIP must be compared with the already published migration and preserved; do not silently drop newer/unrelated changes.

Implement and review dev/resw-localization from actual current main in an isolated new worktree. Integrate only through a reviewed PR with final-head CI and native evidence. Apply the accepted change to each active dev line at its owner-confirmed safe boundary, using normal additive merges or an explicitly reviewed equivalent when ancestry makes a full merge unsuitable. Verify each final branch's preserved feature content, RESW source/embedding/build configuration and relevant tests. Do not count inactive backups or immutable verification snapshots as active rollout failures, but list every disposition and its owner's evidence. Keep #236 open until all active lines are actually read back as converted.

## Verification and evidence

Before conversion, record a full current-main catalog inventory/hashes and run a baseline. Test the new format contract RED before implementing. After conversion, assert exact complete value parity between pinned JSON and RESW, including later main fixes and comments. Run focused and full managed suites on named revisions, normal Release/x64 GUI build and publish, and standalone-tool publishing. Verify all 20 UI RESW files and every value in both build/publish, all 26 Shared embedded resources, each tool's two embedded resources, and the actual compiled PRI contents. Match unchanged installer paths; build the release/MSI only when needed for the accepted native package.

Use task-local SDK8 and VS MSBuild serially. Never reuse the earlier 27-test local-overlay result as a new candidate run. Native EN/RU switching/restart, tray/tool labels and emergency recovery require an immutable package, independent prelaunch review and a NEW exclusive UI grant. Use isolated user state and bounded failure injection. No production settings, hosts, installations or foreign processes are modified implicitly.

Obtain independent whole-change review, required checks on final PR HEAD, actual integrated SHA/tree and post-integration checks. Update contribution/localization docs and a branch-disposition ledger. Accepted issue closure and actual Project Done/readback occur in the same working turn. If Project access, native verification, branch-owner approval or checks remain incomplete, retain explicit open status and do not claim delivery.

## Recovery and unresolved external prerequisites

Recover through additive reviewed commits or restoration of only owned fixture inputs; retain original branch heads and failed evidence. No current implementation exists yet. Actual Project7 admission for #235/#236 and the coordinator's new browser grant are pending. Effective browser policy may still reject GitHub; do not route around a denial. Active owner replies determine the complete rollout set. The user has approved the desired outcome and scope; this document makes the implementation choices reviewable before building.
