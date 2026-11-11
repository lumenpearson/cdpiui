# MainPage wizard navigation verification

## Scope and source checkpoint

[Issue #223](https://github.com/lumenpearson/cdpiui/issues/223) accepts this LOCAL navigation
extraction from frozen baseline `b627a0f62dace8dde20ca2574a9d28646f71b421`, draft PR #222.
The branch is `dev/blockcheck-wizard-navigation` in the existing isolated Root worktree.
Actual Project intake under grant 127 was verified before implementation: In progress, P2,
Architecture, Medium, G2, lumenpearson and two canonical labels after reload. The
[intake receipt](https://github.com/lumenpearson/cdpiui/issues/223#issuecomment-6797095818)
records those values. Queue 128 was released with all six participants paused.

An internal owner now holds the current step, Next enabled state, Next text and Cancel availability.
It reads seven current boolean properties through a direct Session adapter. The existing view,
localizer and command notification remain synchronous dependencies. The public Next properties,
private setters and three private host entrypoints remain forwards. The same CancelCommand reads
the owner's current value; its notification is unconditional, including repeated equal assignments.
One constructor-owned synchronous property bridge preserves the host sender. The main file decreases
from 426 to 395 lines; the four-part MainPage total decreases from 677 to 646.

Only the main existing MainPage source changes, with three new internal source files and focused
tests. Other MainPage partials, Session, options, resources, packages and Core/AddOns/Shared remain
unchanged. The constructor stores all dependencies without reading or calling them. Its placement
before command creation preserves all prior constructor statements and the initial true predicate.

## Managed verification and mutation control

The fresh design check reported "nothing important left" before implementation. The first cold
constructor test observed CS0246 for the missing owner. All behavioral tests were then written
before implementation. The pure owner passed 75/75 in 28 ms before host wiring.

The focused tests include 28 literal visibility rows, equal and unknown steps, nested view calls,
unconditional Cancel notifications, current source reads after callbacks, lazy branches, earlier
step flags, text and property reentry, exception identity, partial effects, retry and instance isolation.
Advanced retains validity then CanContinueFromTargets without a HasTargetSources guard. Next
enabled state remains independent of the separate button visibility during running.

A controlled mutant read CanCancel before the view callback. Five tests failed and 70 passed.
Exact original bytes were restored in finally and verified with SHA256
`0ADBA2B7EF6FDA312B7323B48064A49E0BF3A64CFDDF61D7BAF5B16A92BECCC2`.
The restored focused run passed 75/75 in 25 ms. With host wiring present, the full suite passed
2505/2505 in 9 s, zero failures or skips. The normal WinUI build passed with zero warnings and
errors in 40.39 s.

```text
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BlockCheck2WizardNavigationTests
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore
C:/Users/lumen/.dotnet/dotnet.exe build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore
```

## Compatibility and source guards

The source inventory compares 617 baseline GUI files with 620 current files. All 451 original
exported signatures remain. Compiled comparisons preserve 452 GUI and 88 Core exported type
shapes, with zero removals or changes and zero additions on retained types. These bounded checks
do not certify every attribute, annotation, native ABI or runtime behavior.

The policy guard compares all three moved method bodies with the original tokens under declared
source, view and command substitutions. It reconstructs the complete original host under only the
allowed changes. It verifies seven direct typed mappings, four cold values, two observable setters,
plain step and Cancel values, three forwards, step reads, the same command, one bridge, cold
construction, all prior constructor statements and five additive test links. The guard passed.

The product checkpoint is `31ab5c2f16dbbf11c5c1fab4f5a0e90214be7f05`. The normal GUI SHA256 is
`F1DC9407FF2B5D60F37B682BE9456A04970498F68E0F362F6AE79B8DC8E6644A`. Earlier native evidence
keeps its historical normal-output hash; do not rerun an old verifier against the changed output.

## Native and delivery boundaries

The fresh source fixture is `F:/cdpiui-verify/cdpiui-blockcheck-navigation-31ab5c2-20261111`,
archived from the product pin. Independent pre-build reconstruction verified 1598 Git inputs,
21 inherited cut paths, 35 bootstrap files and 39 allowed extras, with no reparse entries. All 33
prior case partials are equal in raw bytes. Twenty whole cut files are equal; DirectoriesManager
is equal after the exact fixture EXE literal substitution. The accepted 166-label prefix remains,
and six unique navigation groups produce the expected 172-label corpus.

The initial pre-build review found a missing Reporting import in the new scenario. This was a
source-inspected expected compile blocker; no compiler failure was executed. The sole correction
adds that import in the recipe and fixture. Original bytes/hash were preserved. The reviewer
independently verified the exact one-import delta and final case bytes, then returned READY with
zero active Critical, Important or Minor findings. Product, assertions, labels and cuts are unchanged.

The native build passed with zero warnings/errors in 71.55 s. There are 69 approved inputs:
5 owned binaries, 20 generated resources and 44 source/evidence files. All 522 prior output
filenames and hashes form the launch baseline. The 20 packaged resources equal generated bytes;
5206 entries equal the source JSON and all 40 relevant English keys are nonempty. Native GUI DLL
SHA256: `257F9BBF72E47DE7D356CAA5E37AEDF876B635B8CCFF883AF7478A8837271F1E`.
The runner SHA256 is `A1399B0404C63E7064E01BE59FD364575A9BE1C9AA3FFBC7A0CE9CBEA2457F0B`;
the runtime verifier SHA256 is `C653F737B68D91432E8120CCC4E0404F7A8657463BA16150001C22318C702D32`.

### Retained failed native attempt

The first reviewed fixture ran once under explicit grant 129. PID 62736 ran from
2026-11-11T14:04:44.6202465Z to 2026-11-11T14:04:47.2235145+00:00. It passed the prior 166 groups
and the first new group, then failed Completed/Advanced: 167 PASS, one FAIL, no COMPLETE, exit1.
The case had seeded startup grace 0 while expecting valid options. The unchanged product requires
at least 0.25 and correctly returned false. This is a scenario input defect, not accepted runtime
qualification. Independent static reviews had missed that invalid numeric precondition.

All failed fixture/source/archive/case/binary/pin/log bytes remain retained. The failure-specific
readback verified all 69 pins, all 522 prior output hashes, full 1598-input source/cut checks,
exact six new output names, three literal inputs, two golden reports, all seven forbidden paths,
custom-marker absence, normal GUI hash, and owned PID/exact EXE absence. Idle 129 recorded failure,
requestfalse was set, and guarded release 130 retained previousVersion 129 with all six paused.
The initial generic completion verifier was not used to manufacture a successful result.

A fresh `-r2` fixture and `native-r2/` evidence directory retain the same product pin, corpus,
old case bytes and cuts. Only the new case seed changes from 0 to 0.75; an explicit assertion checks
valid options, no targets and the intended CanContinue/CanStart inputs before matrix execution.
Rebuild, independent checks and a new lease apply to that new copy. No rejected fixture is rerun.

### Corrected native build

The R2 build passed with zero warnings/errors in 56.44 s. Fresh R2 pinning again produced
69 inputs, a 522-file baseline and 20 resources with 5206 entries and 40 relevant English keys.
The corrected case SHA256 is `95625539D509B9D2FC603B2A66FA995A55B7F699567D45157309AE5C87B2B70C`.
R2 native GUI SHA256: `A3AE4EDD88451F7C87DF923AA0222F18896BAF8E755A55B6FE04C5CE9D2FB523`.
R2 runner SHA256: `B5BAA80B03C8F3AE1DA2E27B8D59F83A52F8248E1CE169F682F32D738577F35E`.
R2 verifier SHA256: `5182AE4EF0276B8343F7C4F526A1245CEF3F62A734760AE0BD2831509F422189`.
Independent R2 pre-build review checked all numeric limits and all matrix expectations against
the actual options and Session predicates. It returned READY with no active findings.

### Accepted R2 runtime evidence

Independent R2 prelaunch review returned READY with zero Critical, Important or Minor findings.
It rehashed all 69 pins and 522 prior outputs, reconstructed source/corpus evidence and parsed
the actual PE resource tree: AMD64 PE32+, one embedded asInvoker/uiAccess=false manifest.
The current token was non-admin; the exact process and fresh outputs were absent. PowerShell
parsing had zero errors. This static review did not replace the subsequent runtime assertions.

After Root's own immediate hash/readiness checks, explicit grant 131 permitted one R2 process.
PID 89288 ran from 2026-11-11T14:14:02.1120838Z to 2026-11-11T14:14:04.6887812+00:00. All 172 unique groups passed,
with the exact 166/160/154/149/137 prefixes and peer 141 projection, one COMPLETE, zero FAIL,
exit0 and empty streams. All 69 pins, 522 prior filenames/hashes, full source boundaries and
resources passed afterward. Exactly six new names made 528 total files. The three input files
retain their literal 16/16/15 bytes. Both serializer reports retain exact 354/260-byte golden
contents and current model fields. All seven forbidden paths and custom markers remain absent.
The normal GUI DLL retains the recorded F1DC9407 hash.

The six groups assert cold fields and both private setters, host sender/equality, command identity
and unconditional equal-value notifications, all step/running/Canceling visibility and availability,
invalid advanced options and missing connection/target/report branches, view mutation before
current CanCancel, command mutation before later source reads, retained earlier step flags,
nested command frames, separate owners, equal/unknown/nested steps, view exception identity,
command-observer partial state and retry. Actual MainPage, Session, source, owner, command,
localizer and dispatcher are exercised. New view endpoints are controlled callbacks; settings
constructor reads use the existing inert port. No OnLoaded, Execute, settings write, scan,
backend, history, cleanup, additional operating IO or source cut is introduced.

Root independently checked PID 89288 and exact EXE absence, wrote idle 131/requestfalse with
no pending call, and read back guarded release 132 with previousVersion 131, owner null and
all six participants paused. R2 evidence is in this plan's `native-r2/` subdirectory. The initial
failed fixture and all original review/build/runtime records remain retained separately.

Fresh whole-branch review, exact final-head hosted checks, actual PR Project readback and
immediate LOCAL acceptance remain pending at this evidence checkpoint.

Refs #14 and Refs #223. Parent #13/#14/#15 and draft PRs #216/#218/#220/#222 remain separate and
unmerged. Model composition does not establish full Page rendering, XAML Loading, focus,
activation, installed-product, GC collection, G1 or release acceptance. Retain all source archives,
refs, workspaces and accepted or rejected evidence. Raw evidence belongs to the ignored workspace
`.superpowers/sdd/2026-11-11-blockcheck-wizard-navigation/`.
