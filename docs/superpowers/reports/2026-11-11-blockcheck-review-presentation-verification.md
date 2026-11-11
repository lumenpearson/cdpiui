# MainPage review presentation verification

## Scope and source checkpoint

[Issue #221](https://github.com/lumenpearson/cdpiui/issues/221) owns this LOCAL review-summary
extraction. Frozen baseline: `3c7f8961477dbff0b4b021493b629ba1a1a0fa05`, draft PR220.
Design commit: `330458e623ead885a632c5ff0abd5e7fd9bbb739`. The isolated branch is
`dev/blockcheck-review-presentation` in the Root worktree. Actual Project intake119 was verified
before implementation, then released120 with all six participants paused. The
[intake receipt](https://github.com/lumenpearson/cdpiui/issues/221#issuecomment-6795366566) records
In progress/P2/Architecture/Medium/G2/lumenpearson and two canonical labels after reload.

An internal six-string owner now formats the same summary through a15-property read-only source,
a direct Session adapter, localized Text and the existing duration formatter. The six public host
getters/private setters and private supplied-preview method remain forwards. One constructor-owned
synchronous bridge retains the host sender and current-field semantics. The four MainPage partials
decreased from740 to677 lines; the Review partial decreased from110 to43.

The existing product footprint is exactly MainPageViewModel.cs and Review.cs. Three internal files
are added: the source interface, its direct Session adapter and the presentation owner. Session,
options/site lists, numeric/progress/completion owners, duration and preview models, lifecycle,
navigation, resources, packages, Core/AddOns/Shared and operating code remain unchanged.

## Design and managed evidence

A fresh design-builder inspected the policy, constructor, notification base and inherited160 fixture
boundaries, and reported "nothing important left" before implementation. The source contract uses
the original getter types; SelectedRunPreset is non-nullable. The intake verification wording was
corrected to distinguish an unknown preset from null preview/estimate cases.

The first constructor test was written before any interface/adapter/owner implementation. Its
focused run observed the expected CS0246 missing BlockCheck2ReviewPresentation. All subsequent
behavior tests were also written before the implementation. The first owner run passed187/187
in38ms. These tests include128 literal protocol/IP input-output rows and discriminating checks for
localization, lazy reads, culture, current values after callbacks, partial failures and preview identity.

A controlled mutant cached SelectedRunPreset before the first display update. It failed12 cases and
passed175, including stale mode state and early read-order assertions. Exact original bytes were
restored in a finally block and verified with SHA256
`9F5A14E3FA90599EBDF8B74FA1701AB78A7CAD1EB5CEB9DBFF7890287C13BC36`.
The restored focused run passed187/187 in33ms. With host wiring present, the full suite passed
2430/2430 in9s, with zero failures/skips. The normal WinUI build passed0 warnings/errors in39.32s.

```text
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~BlockCheck2ReviewPresentationTests
C:/Users/lumen/.dotnet/dotnet.exe test CDPIUI.Tests/CDPIUI.Tests.csproj -c Release --no-restore
C:/Users/lumen/.dotnet/dotnet.exe build CDPIUI/CDPIUI.csproj -c Release -p:Platform=x64 --no-restore
```

The focused assertions cover all protocol/IP selections, null/empty automatic TLS names, explicit
preset branches and unknown fallback, the short-circuited second preset read, localization mutation,
publication mutation, culture after numeric getters, null preview and invalid formats, positive
maximum-duration formatting with distinct counts, repeated estimate reads after a duration callback,
nested updates with different previews, six setters/equality/current values, all15 getter failures,
observer and formatter exception identity, partial effects, retry and independent owners.

## Compatibility and exact-source guards

The source inventory compared614 baseline GUI files with617 current C# files. All451 original
exported source signatures remain. Compiled comparisons preserve452 GUI and88 Core exported type
shapes, with zero removals/changes and zero additions on retained types. These bounded inventories
do not certify all annotations, attributes, native ABI or runtime behavior.

The policy guard compares original tokens under the declared source/Text/FormatDuration mappings,
including significant strings and repeated preview reads. It verifies all15 exact typed source
getters, cold constructors/fields, six owner and host setters, the private preview forward, one bridge,
all prior constructor bytes and the three additive test links. The adapter does not copy values or
read properties during construction. The guard passed without a product correction.

The accepted previous normal GUI DLL was saved before rebuilding. The current normal GUI SHA256 is
`A0D830364F9A25B7ACD755E71DC1B704CE47DEA261C11A68B7589F3C8FCF5753`.
Earlier160 runtime evidence retains its historical normal-output hash. Do not run its frozen
verifier against this new normal output and describe that result as a regression of the old fixture.

## Native and delivery status

Native qualification is pending at this product checkpoint. The next stage must create a NEW source
archive, preserve all160 accepted labels/old case bytes/21 cuts, and add actual MainPage/source/
preview/bridge cases. Positive maximum formatting, callbacks, private setters, equality, sender,
reentry and partial exceptions need direct assertions. Derive fresh counts/pins/outputs from that
fixture, obtain independent pre-build/prelaunch READY, then acquire a new explicit exclusive lease.
Root currently has no ready native request or pending UI call.

Final whole-change review, stacked draft final-head checks, actual PR Project readback and immediate
completed/Done acceptance remain pending. Parent13/14/15 and drafts216/218/220 stay separate and
unmerged. No full Page rendering, XAML Loading, installed product, GC collection, G1 or release
result is inferred. Keep all accepted and rejected evidence, branches, workspaces and source archives.
