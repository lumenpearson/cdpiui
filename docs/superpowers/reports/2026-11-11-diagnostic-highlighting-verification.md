# Diagnostic highlighting update verification

Issue: [#215](https://github.com/lumenpearson/cdpiui/issues/215).
Execution owner: Root, `dev/audit-class-decomposition`,
`C:/Users/lumen/.codex/worktrees/audit-class-decomposition/cdpiui`.
Product checkpoint: `ed390cd416b58ea404f04286f7eace5a66e4320a`.
This is a local implementation stage. Integration remains in #14 and broader native acceptance in #15.
The local stage is accepted: final independent review passed, issue215 is closed as completed with
all six criteria checked, and actual Project Status Done was verified after reload under queue85.

## Behavior and scope

`ConfigMakerUserControl.Editor.cs` delegates its one highlighting signature and reset operation to
the internal System-only `DiagnosticHighlightingUpdater`. The editor file changes from 157 to 149
lines; the five control partials change from 828 to 820. The helper contains 29 lines.
The control retains its background/light calculation and literal highlighter/Design setter order.

The extraction preserves two current diagnostic reads, the complete error pass before the warning
pass, separate predicate/token Source reads, raw token order/duplicates/case/null values, literal
comma/pipe signature collisions and Ordinal comparison. Collection completes before the current
key is compared. The requested key is committed before apply. A failed apply keeps that key.
Reentrant callbacks and collection-time reset/reentry retain their original state effects.
No view model, XAML, preference implementation, parser or persisted format is changed.

## Managed checks and contracts

Windows, .NET SDK 8.0.427 at `C:/Users/lumen/.dotnet/dotnet.exe`:

- Missing-helper RED: CS2001 before the helper was created.
- Focused tests: 32 passed. A controlled commit-after-callback mutation caused 4 failures; the
  original bytes were restored in `finally`, then all 32 passed again in 23 ms.
- Restored helper SHA256: `88721943DD1D7520EAE6846A236AEC2CC60575ACEF74B2AE2DFBD040A9C6CA5F`.
- `dotnet test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore`: 2108 passed,
  0 failed/skipped, 10 seconds.
- `dotnet build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore`: 0 warnings/errors,
  42.76 seconds.
- GUI source inventory: 451 original exported types across 517 baseline to 606 current C# files,
  0 removed/changed original signatures. All 606 current source inputs match the committed pin.
- GUI compiled inventory: 452 original exported types, 0 removed/changed shapes, 7 previously
  recorded additions. Source and metadata inventories do not establish full native ABI compatibility.
- Core source is unchanged from accepted `00f39b1a9be70cb97cebd7290b5f24dac6a5ab47`.

Normal GUI DLL SHA256: `100E51E7FBD990AAB9D41755769796CC6C4EA7B75F79BCFF8C7894C5F96FA3EC`.
Raw evidence remains under `.superpowers/sdd/2026-11-08-class-decomposition/`, including
`task89-*`, `api-compatibility/*highlighting*` and `compiled-contract/highlighting-GUI-*`.

## Committed source composition

The Core-inclusive builder creates fresh copies from exact committed blobs, preserves the security
test items and verifies all repository inputs plus the additive test-project XML before and after
execution. It does not modify another session's worktree or Git references.

Security composition with `ee4771675956fe213634675f37579362efb7e0eb`:
`F:/cdpiui-verify/joint-highlighting-ed390cd-security-a`.
2320 tests passed, 0 failed/skipped; GUI 0 warnings/errors, 59.09 seconds.
The proof covers 1546 expected paths, 1545 Git blobs, 184 Root overlays, 112 additive project items
and all 14 security items. Only the test project overlaps. The generated empty secret stub is the
only extra source input. Post-build source proof passed.

The corrected peer composition with `f706f16934695bcb122de63d0442c4ab20f42869` passed:
`F:/cdpiui-verify/joint-highlighting-ed390cd-peer-f706f16-a`, 2398 tests, 0 failed/skipped;
GUI 0 warnings/errors, 58.74 seconds. All 1552 expected paths and 1551 Git blobs are covered,
with 184 Root overlays, 10 peer delta paths, 115 deduplicated additive items and all 14 security
items retained. The post-build source proof passed. That peer pin includes the complete dispatcher
bridge correction; rejected `9e63b55` is not accepted. The fresh independent reviewer reconstructed
both complete trees and XML from committed blobs and hashed the disk contents, without joint
findings. These results do not establish a combined native runtime pass.

## Native fixture status and limits

The first independent preparation review found an Important error in three new fixture assertions:
the pinned library exposes `SyntaxHighlightLanguage.Highlights` as `SyntaxHighlights[]`, so the
assertions must use `Length`. The original case/manifest/diff are retained in
`highlighting-prebuild-count-review-original`. Only the three `Count`→`Length` assertions were
corrected in scratch and the unbuilt fixture; full source-boundary checks passed again. This was
found by reading library metadata before any fixture build. Independent correction readback confirmed
exactly those three changes and returned READY for BUILD, with 0 Critical/Important findings.

The first compilation then failed with CS0246 because the new fixture partial lacked the
`CDPIUI.ViewModels.ConfigMaker` import for `IConfigMakerView`; the XAML target reported the same
compiler failure. The original case/manifest/diff and `highlighting-host-build.log` are retained.
Only that import was added in scratch and the fixture; all source-boundary checks passed again.
The second compilation passed with 0 warnings/errors in 48.44 seconds, retained separately in
`highlighting-host-build2.log`. These fixture-only corrections do not alter the product checkpoint.

The fixture archive is `F:/cdpiui-verify/cdpiui-highlighting-ed390cd-20261111`.
The source checker compared all 1531 repository inputs and 1246 GUI/Shared/Core/AddOns inputs.
It found exactly 21 boundary files and 28 bootstrap files, with all 26 previous case partials byte
identical to accepted Root menu133. These are preparation results, not an executed native test.
The binary directory has 522 files. Five actual binaries and 18 source inputs are pinned (23 total).
The fixture DLL SHA256 is `4B8F56E35BD74C441E53D3D3534699C2FD8856AE74CAF1DF4BBAC10009E36D83`.
The runner SHA256 is `FE83083A54CE84B55BD9922C0DD34A9FA514EEFF7B9F8E747FB01000CC430A22`;
the readback verifier SHA256 is `FBC16112E73BAC5E0D6C881AF5D84863E4739A1B21DF4B273BB8D23642A5F08F`.
These pins belong to the first compiled attempt, which must remain unlaunched. Independent product
review found one Important fixture attribution error: cumulative preference reads/writes cannot
prove that this unloaded control initialized the font singleton. Generated x:Bind waits for Loading.
The first attempt remains intact at its original path, with its sources, binaries, pins and logs.

A fresh `-r2` archive uses incremental per-key Search reads around actual control construction,
plus a separate explicit `ConsoleFontHelper.Instance` check of the three fixed typed font values
and two bounded FontSize setter writes in memory. It will not attribute font access to delayed
control bindings. Both preference cuts remain. Its first archive-preparation command stopped on
the clean-worktree guard before it created the archive: current documentation was untracked.
Documentation checkpoint `5805c8334c76d95d9bd301ed423b7cb7844b02ae` resolved that guard without
weakening it; product inputs remain the literal `ed390cd`. Independent r2 source review returned
READY for BUILD after reconstructing all 1531 inputs, 21 cuts, 28 bootstraps and 26 old case files.
The r2 build passed with 0 warnings/errors in 69.10 seconds. A separate independent post-build
review verified all 23 pins, actual embedded asInvoker/uiAccess=false manifest, fresh 522 binary
filenames, non-admin execution, local NTFS/no reparse points, exact source and uncut Editor/updater,
and the runtime gates. It returned READY with 0 Critical/Important findings.

The r2 fixture is `F:/cdpiui-verify/cdpiui-highlighting-ed390cd-20261111-r2`.
DLL SHA256: `A03599B0A1F5C3F05F0930BAB381DB1E0A9C84CC435BDE04F0387EA6C2ADB70F`.
Runner2 SHA256: `83B1319277F3C6448A0500C7A86D7CB482F415E5EA8F11DCC9CC9259A676DED9`.
Verifier2 SHA256: `912D39C1279C84C7CF3751DF8699FBC281CB7C1D19B09C19698631AD2FC3655C`.

The canonical request was atomically updated to r2 READY, then an explicit guarded free82→Root83
grant was read back, with every other UI participant paused. One hidden owned process ran from
2026-11-11T07:00:36.6419523Z to 07:00:42.8313907+00:00, PID79052, exit0. All 137 unique groups
passed with the accepted133 prefix in exact order, one COMPLETE, no FAIL, and empty stdout/stderr.
Postchecks verified all 23 actual pins, five binary hashes before/after execution, full source,
three retained literal inputs of 16/16/15 bytes, two real serializer outputs of 354/260 bytes with
their exact golden hashes and model fields, 522→528 filenames with only the six approved additions,
all prior names retained, forbidden outputs absent, unchanged normal GUI and termination of the
owned process. The idle receipt/requestfalse/all-UI-paused state preceded guarded release83→84,
owner null. No CUA, desktop input or foreign window action occurred. All V1 pins remain unchanged.

Runtime evidence: `highlighting-host2-execution.log`, `highlighting-host2-run.log`,
`highlighting-host2-run-manifest.txt`, `highlighting-host2-{inputs,outputs}-retained.json`,
`highlighting-host2-postchecks.json`, `highlighting-host2-approved-pins.json`,
`highlighting-host2-explicit-grant-v83.json` and `highlighting-host2-owner-released-v84.json`.

The old 19 boundary files remain. Two explicit fixture-only additions replace only preference
endpoints in `SearchUserControl.xaml.cs` and `ConsoleFontHelper.cs` with typed in-memory values.
They reject unknown keys/types; neither patched file retains a SettingsManager call. The actual
constructors, setters, XAML, editor and new updater remain. No product preference code is changed.
The Search constructor reads preferences immediately. The generated control binding defers font
access to Loading, so this unloaded control does not prove that it initializes the font singleton.
The fixture exercises that singleton and its setters explicitly as a separate isolated boundary.

Four new executed groups extend Root menu133 to Root137: constructor/unchanged identity plus the
separate explicit font boundary, current
diagnostic Source/severity, actual background reset, and independent caches in two owned controls.
Root137 differs from the peer named-value137 and the peer dispatcher141. Counts do not establish
combined runtime acceptance. All owned controls stay unloaded/unattached; owned timers are stopped.
This does not establish full event detachment or garbage collection: the existing
`AnimatedSelectorBar` subscribes to a static item event in its constructor and unsubscribes on
Unloaded. This fixture deliberately does not raise Unloaded, so that subscription retains its
objects until the owned bounded process exits. No global handler is changed by the test.
No desktop input, activation, navigation, focus, backend or preference IO is permitted. Managed tests
cover fault/reentry ordering; this bounded native fixture does not prove full visual/startup or
installed-product behavior. Source/prelaunch reviews and runtime postchecks passed.

## Final local acceptance

Fresh read-only reviewer `/root/highlighting_integration_review` accepted product
`ed390cd416b58ea404f04286f7eace5a66e4320a` and final technical documentation
`cee65ac3c82d53d2a7b322e0299b1fb50271f2cb`: 0 unresolved Critical, Important or Minor findings.
Product, both joint trees/XML, actual r2 runtime/IO/hash/process/queue evidence and the corrected
font timing/first-copy status were independently checked. The existing bounded selector lifetime
limit remains documented. All 141 rulings and 18 deferred Minor records match the ledger literally.

The six-section [acceptance record](https://github.com/lumenpearson/cdpiui/issues/215#issuecomment-6789505833)
preceded immediate API closure as completed and six checked criteria. Actual Project85 readback
after reload confirmed Done/P2/Architecture/Medium/G2/lumenpearson and exactly
`type:refactor`, `area:architecture`, `area:native-qa`; only Status changed. Proof:
`C:/Users/lumen/Programming/cdpiui/.git/task-policy-rollout-2026-11-10/issue215-closeout/project-215-final-readback.json`
and `issue215-done.jpg`. The metadata owner released85→86 with all Root UI calls paused.
Parent14/15 were read open and remain separate integration/native scopes. This closing documentation
does not change the reviewed product, tests or runtime artifacts; no additional build is needed.
