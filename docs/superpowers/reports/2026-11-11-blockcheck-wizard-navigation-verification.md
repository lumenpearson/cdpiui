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
One constructor-owned synchronous property bridge preserves the host sender.

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

## Native and delivery boundaries

Fresh native qualification, independent whole-branch review, exact final-head hosted checks,
actual PR Project readback and immediate local acceptance remain pending at this checkpoint.
The next fixture must preserve all accepted 166 labels, 33 old case partials and 21 cut paths.
It must obtain independent pre-build and prelaunch review and a new explicit exclusive UI lease.
No OnLoaded, command Execute, real scan, settings write, backend, history or cleanup is required.

Refs #14 and Refs #223. Parent #13/#14/#15 and draft PRs #216/#218/#220/#222 remain separate and
unmerged. Model composition does not establish full Page rendering, XAML Loading, focus,
activation, installed-product, GC collection, G1 or release acceptance. Retain all source archives,
refs, workspaces and accepted or rejected evidence. Raw evidence belongs to the ignored workspace
`.superpowers/sdd/2026-11-11-blockcheck-wizard-navigation/`.
