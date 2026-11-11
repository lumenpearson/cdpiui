# MainPage review presentation verification

## Scope and source checkpoint

[Issue #221](https://github.com/lumenpearson/cdpiui/issues/221) owns this LOCAL review-summary
extraction. Frozen baseline: `3c7f8961477dbff0b4b021493b629ba1a1a0fa05`, draft PR #220.
Design commit: `330458e623ead885a632c5ff0abd5e7fd9bbb739`. The isolated branch is
`dev/blockcheck-review-presentation` in the Root worktree. Actual Project intake under grant 119 was verified
before implementation, then released 120 with all six participants paused. The
[intake receipt](https://github.com/lumenpearson/cdpiui/issues/221#issuecomment-6795366566) records
In progress/P2/Architecture/Medium/G2/lumenpearson and two canonical labels after reload.

An internal six-string owner now formats the same summary through a 15-property read-only source,
a direct Session adapter, localized Text and the existing duration formatter. The six public host
getters/private setters and private supplied-preview method remain forwards. One constructor-owned
synchronous bridge retains the host sender and current-field semantics. The four MainPage partials
decreased from 740 to 677 lines; the Review partial decreased from 110 to 43.

The existing product footprint is exactly MainPageViewModel.cs and Review.cs. Three internal files
are added: the source interface, its direct Session adapter and the presentation owner. Session,
options/site lists, numeric/progress/completion owners, duration and preview models, lifecycle,
navigation, resources, packages, Core/AddOns/Shared and operating code remain unchanged.
Product checkpoint: `1c2d44336fe4bb97bc100a5102fc7a7667d65068`. Later evidence commits do not
change the qualified product or native case bytes.

## Design and managed evidence

A fresh design-builder inspected the policy, constructor, notification base and inherited160 fixture
boundaries, and reported "nothing important left" before implementation. The source contract uses
the original getter types; SelectedRunPreset is non-nullable. The intake verification wording was
corrected to distinguish an unknown preset from null preview/estimate cases.

The first constructor test was written before any interface/adapter/owner implementation. Its
focused run observed the expected CS0246 missing BlockCheck2ReviewPresentation. All subsequent
behavior tests were also written before the implementation. The first owner run passed 187/187
in 38 ms. These tests include 128 literal protocol/IP input-output rows and discriminating checks for
localization, lazy reads, culture, current values after callbacks, partial failures and preview identity.

A controlled mutant cached SelectedRunPreset before the first display update. It failed 12 cases and
passed 175, including stale mode state and early read-order assertions. Exact original bytes were
restored in a finally block and verified with SHA256
`9F5A14E3FA90599EBDF8B74FA1701AB78A7CAD1EB5CEB9DBFF7890287C13BC36`.
The restored focused run passed 187/187 in 33 ms. With host wiring present, the full suite passed
2430/2430 in 9 s, with zero failures/skips. The normal WinUI build passed 0 warnings/errors in 39.32 s.

```text
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BlockCheck2ReviewPresentationTests
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore
C:/Users/lumen/.dotnet/dotnet.exe build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore
```

The focused assertions cover all protocol/IP selections, null/empty automatic TLS names, explicit
preset branches and unknown fallback, the short-circuited second preset read, localization mutation,
publication mutation, culture after numeric getters, null preview and invalid formats, positive
maximum-duration formatting with distinct counts, repeated estimate reads after a duration callback,
nested updates with different previews, six setters/equality/current values, all 15 getter failures,
observer and formatter exception identity, partial effects, retry and independent owners.

## Compatibility and exact-source guards

The source inventory compared 614 baseline GUI files with 617 current C# files. All 451 original
exported source signatures remain. Compiled comparisons preserve 452 GUI and 88 Core exported type
shapes, with zero removals/changes and zero additions on retained types. These bounded inventories
do not certify all annotations, attributes, native ABI or runtime behavior.

The policy guard compares original tokens under the declared source/Text/FormatDuration mappings,
including significant strings and repeated preview reads. It verifies all 15 exact typed source
getters, cold constructors/fields, six owner and host setters, the private preview forward, one bridge,
all prior constructor bytes and the three additive test links. The adapter does not copy values or
read properties during construction. The guard passed without a product correction.

The accepted previous normal GUI DLL was saved before rebuilding. The current normal GUI SHA256 is
`A0D830364F9A25B7ACD755E71DC1B704CE47DEA261C11A68B7589F3C8FCF5753`.
Earlier160 runtime evidence retains its historical normal-output hash. Do not run its frozen
verifier against this new normal output and describe that result as a regression of the old fixture.

## Actual native qualification

The fresh fixture is `F:/cdpiui-verify/cdpiui-blockcheck-review-1c2d443-20261111`, archived from
the product checkpoint. It contains 1591 tracked Git inputs, exactly 21 inherited cuts, 34 bootstrap
files and 38 allowed extras, with no reparse entries. All 32 prior case partials retain their bytes.
The main bootstrap adds the last review call and COMPLETE166 count, apart from path/pin names.
The exact 160-label prefix is retained and six distinct review groups are appended.

Before the initial review verdict, the adapter group gained explicit Quick/TestAllStrategies true
and false assertions with all protocol flags false, plus the unknown-preset Balanced fallback. The
seven-line assertion delta changed no label, product, operating endpoint or cut. Original draft
case bytes were retained. The final case SHA is recorded below. The independent pre-build reviewer
reconstructed the complete fixture and approved those final bytes with 0 Critical/Important/Minor.

The native build passed 0 warnings/errors in 75.27 s. Independent prelaunch review rehashed all 65
approved inputs and all 522 output paths/digests, reconstructed source/manifest/corpus evidence and
verified all 32 prior partials again. The actual executable is PE32+ AMD64, with an embedded
asInvoker/uiAccess=false manifest. The token was non-admin and the exact executable was absent.
PowerShell parsing had 0 errors; the runtime verifier and pin tables were consistent. All 7 forbidden
paths, 13 fresh run/evidence paths and custom markers were absent. No pending hash value remained.

The 65 approved inputs are 5 owned binaries, 20 resources and 40 source/evidence files. All 20 resources
are generated-byte identical, their 5206 entries equal source JSON, and 38 required English keys are
nonempty. Selected actual hash anchors:

| Artifact | SHA256 |
| --- | --- |
| Native GUI DLL | `AFBD9A8DAEEF9B4F386F8FB0CFD6267C1AB6A064F8DC05A1963F530AB6095F15` |
| Runner | `90DDD4A03497353A89058C7999717DBDBA19818099BA2B838723398420BB7F9A` |
| Runtime verifier | `CB412BD26DF46368A4B8F035B9F0519B2E0E73B43F0E105E58C643BBCD07D478` |
| Complete output baseline | `9585290814A1CD60E09E02B03642FF5A6CEE7E1E70CB3250B842580E98B8D8E8` |
| Accepted corpus | `7BDB3893F3D0E3F179365AD24EF3941E74DD4E1A4C4C15A2E45F518130E0C81A` |
| Final review case | `09B6F291DF5D2A93EF0C97AE81FCEBAA40CC549CBFD7583E87E8AE31A75479BB` |

Root independently rehashed the 65 pins and 522 baseline files immediately before guarded queue
transfer 120в†’121. Only this native process owned that lease. PID 6720 ran from
2026-11-11T12:44:37.6688885Z to 2026-11-11T12:44:40.2506392+00:00. All 166 unique groups passed
with the exact 160/154/149/137 prefixes, peer141 projection, one COMPLETE, zero FAIL, exit0 and
empty stdout/stderr. All 65 pins/all 522 prior filenames and hashes passed after exit. Exactly six
new output names made528 total. The three literal inputs retain16/16/15 bytes. Both serializer
reports retain exact 354/260-byte golden contents, hashes and current model fields. All 7 forbidden
paths remain absent, including both non-file list paths. The normal GUI remains at the SHA above.

The six new groups directly assert cold values, all private setters/host sender/equality, individual
protocol/IP mappings and full order, 2 selected lists/5 domains/23 preview targets, distinct request/
timeout values and positive 61-minute duration with 17 jobs/199 requests. They assert both explicit
all-strategies states, unknown preset fallback, callbacks changing later source/preview reads,
repeated equality/current adapter values/separate owners, nested preview identity and observer
exception identity/partial effects/retry. They use actual MainPage, source adapter, Session, preview,
localizer, duration formatter and dispatcher with a two-method view substitute that is never called.

Both list items receive nonempty literal content, so their constructors use the in-memory domain
count branch. Reflection seeds option fields and avoids persisted setters. All externally attached
new handlers detach in finally. Operating counters/settings writes remain unchanged; existing
inert settings constructor reads remain part of composition. No OnLoaded, real scan/preparation/
backend/history/cleanup operation, settings write, additional operating file IO, desktop input or deletion
was introduced. External absence checks cover the two non-file paths.

Root separately verified PID 6720 and the exact executable absent, wrote idle 121/requestfalse with
no pending call, and guarded release 122/readback confirmed owner:null and all six participants paused.
Raw recipes, logs, pins, baseline, source/resource evidence and postchecks remain in this plan's
ignored workspace `.superpowers/sdd/2026-11-11-blockcheck-review-presentation/`.

## Independent final review

Fresh reviewer review_presentation_final_review examined the complete range
`3c7f8961477dbff0b4b021493b629ba1a1a0fa05` through
`b679c6c10cde31ce728fb233f505450f57adeaa7`: zero Critical, zero Important and two Minor.
The product policy, direct adapter and synchronous bridge preserve the original behavior. Independent
read-only checks rehashed all 65 pins and 522 prior outputs, matched six new names, inputs and reports,
and reconstructed the exact 166-label corpus. Owner logs remain the executed build/test/process
evidence; the reviewer did not rerun those commands.

Both findings concerned records. The retained ledger now qualifies its earlier shorthand: all 21
cut paths match, 20 complete files are byte-identical, and DirectoriesManager changes only the new
expected fixture EXE path. Replacing that literal reproduces the whole file. All 32 old case partials
are byte-identical without substitution. The three new documents also separate ordinary words,
counts and issue references, including explicit Refs #14 and Refs #221. Product, tests, native case,
pins and outputs are unchanged. Static document checks apply to these corrections.

The reviewer supports the stacked draft and qualified local result. Full Page/installed behavior,
runtime GC collection and real operating routes remain outside the bounded fixture. Final remote
checks and actual private Project readbacks remain executor gates before local closure.

## Delivery status and limits

Stacked draft final-head checks, actual PR Project readback and immediate
completed/Done acceptance remain pending at this evidence checkpoint. Parent issues #13/#14/#15 and draft PRs
#216/#218/#220 stay separate and unmerged. The two-method view establishes model composition, without
full Page rendering/XAML Loading/focus/activation, installed-product, GC collection, G1 or release
acceptance. Independent qualification of unchanged components remains with their owning stages.
Keep all accepted and rejected evidence, branches, workspaces and source archives.
