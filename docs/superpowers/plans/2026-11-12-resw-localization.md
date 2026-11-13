# RESW localization and frozen JSON beta implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Prepare a frozen JSON beta and a reviewed RESW stable candidate with all accepted behavior preserved, then roll it out only through admitted integration boundaries.

**Architecture:** Central editable RESW catalogs supply direct WinUI Content/PRI resources and embedded Shared/tool resources. A secure XML catalog reader replaces the JSON reader; locale choice and all current GUI/tray/tool fixes stay in place.

**Tech Stack:** C#/.NET 8, WinUI3Localizer, MSBuild, XML RESW, xUnit, PowerShell.

**Spec:** docs/superpowers/specs/2026-11-12-resw-localization-separation-design.md (approved design copied from the external design.md).

## Global constraints

- Actual base fe29acfc4238228da3a6aebb98fff4345c3da106; no old local-overlay substitution.
- Local preparation without Project7 is explicitly authorized by the latest user message. Main merge, native acceptance and completed/Done closure remain gated by actual Project access and full acceptance.
- Wait for actual release of native slot211 before branch/code/heavy work; retain allUIpaused, pendingUI/SDK/native0, no automatic UI resume.
- Preserve every frozen owner-confirmed ref, occupied checkout, index and accepted fixture. Only Root writes owner.json. All heavy commands run serially in a coordinated CLI window.
- No credential input. Use the documented empty Secret.cs stub only in new owned worktrees.
- Current catalogs:26/5330 entries; exact key/value and newline parity is required.
- Standalone tools retain linked source and only their own embedded EN/RU catalogs, with no new deployment DLL dependency.
- Preserve unrelated Store/preset JSON and historical evidence documents.

## Review focus

- A missing or malformed Russian resource must use readable English, and failure of both resources must show an ID without recursive logging/UI access.
- DTDs, duplicate names, invalid roots and oversized XML must not resolve external data or poison a partially read cache.
- A normal clean/incremental build and publish must include every expected Strings path and compiled Resources PRI input without the removed generator.
- Tool launches must retain selected-language propagation and standalone packaging without loading from mutable install files.
- Current main caption/toggle/startup fixes and all branch-specific decomposition inputs must survive format replacement and future owner-controlled adoption.

## Task 1: Preserve beta and baseline

**Files:** Create beta README; copy approved design/plan and branch ledger in the owned stable worktree. Product files initially unchanged.

**Interfaces:** Produces frozen beta ref and a machine-readable current-main catalog manifest for Task2. Consumes owner receipts and actual slot release.

- [x] Read actual released queue state and obtain serial CLI scheduling acknowledgement. Create native-managed beta and stable worktrees at fe29; name branches dev/beta-json-localization and dev/resw-localization. Verify primary index/source/ref preservation.
- [x] Run the complete current-main managed suite with a unique TRX under repository ancestry. Expected: all tests pass, zero skips; preserve any failed attempt.
- [x] Build/publish normal baseline GUI and record the20 delivered catalogs/compiled PRI and26 embedded JSON resources. Expected: normal exit0 and complete current-main values.
- [x] Add beta freeze documentation explaining feature boundary, exact base and no automatic stable merge. Commit only beta docs with Refs #235. Keep baseline evidence separate from later RESW results.

## Task 2: Replace authoring format and embedded runtime

**Files:** Localization/{en-us,ru}/*.resw replaces *.json; CDPIUI.Shared/Localization/{ReswCatalog,ReswLocalizer}.cs replaces JSON classes; four project files and their call sites; remove build generator/targets. Adapt Shared localization tests and add source-format/packaging tests.

**Interfaces:** ReswCatalog(Func<string,string,Stream?>).Get(string catalog,string key,string language) and public ReswLocalizer.Get(string catalog,string key,string? language=null). LocalePreference remains unchanged. Embedded names Localization.<language>.<catalog>.resw.

- [x] Write a source-format test expecting26 RESW inputs/no application JSON or generator, and convert existing fallback fixtures to valid XML while retaining the old loader for RED. Add invalid root/duplicate name/DTD/size/repeated-cache tests. Run the relevant tests. Expected: assertion failures because the source/reader is still JSON; no compile/import failure is accepted as RED.
- [x] Convert pinned current-main catalogs with XML escaping/xml:space and exact CR/LF preservation. Include legacy translator comments. Expected: machine comparison26 catalogs/5330 entries, zero changed/missing values.
- [x] Implement secure bounded XmlReader-based ReswCatalog, rename the public adapter, replace consumers and manifest names, and preserve LocalePreference plus all unrelated fixes. No catch-all for process-fatal exceptions.
- [x] Replace GUI import with direct Content/PRI items using the existing output/publish Strings layout; change Shared/tool embedding to RESW; delete only JSON-specific generator files and editable JSON catalogs.
- [x] Adapt catalog/localizer tests to RESW and run focused tests, then the full suite. Expected: green source format, fallback/XML security, per-entry embedded parity, caption/toggle/recovery tests and complete suite with zero skipped cases.
- [x] Commit the coherent RESW implementation and regressions with Refs #236; retain RED/GREEN commands and results in the ledger.

## Task 3: Verify artifact composition and review

**Files:** Localization/README.md, contribution guides, specs/plans, branch disposition/evidence report; no unrelated product fixes.

**Interfaces:** Consumes Task2 stable resource contract; produces tested candidate SHA, complete evidence and reviewed feature-only beta comparison.

- [x] Update editing, output/embedding, fallback, tool and beta instructions. Keep old historical test/review records untouched.
- [x] Run normal Release/x64 GUI build/publish, tool publishing and catalog inventory checks. Expected:20 UI RESW/5206 entries at source/build/publish,26 embedded Shared catalogs/5330 entries, two catalogs per tool, current compiled PRI values and unchanged installer paths.
- [x] Compare beta/stable catalogs and product paths, verify no JSON backend/generator dangling references and no unrelated product delta. Expected: all translation values/common fixes retained; only the format/backend/test/doc boundary differs.
- [x] Run relevant governance checks and whitespace validation. Commit documentation/evidence. Obtain fresh independent whole-branch review using the approved spec and review-focus list; resolve material findings through RED->GREEN and a green full suite.
- [x] Record exact final SHA and checks. Native/UI, Project fields, hosted PR gates, main merge and closure remain pending if access/admission is unavailable; do not claim complete delivery.

## Task 4: Integrated owner-controlled rollout (admission dependent)

**Files:** Branch-disposition ledger and each active owner's accepted stable adoption; frozen evidence inputs excluded by explicit receipts.

**Interfaces:** Consumes reviewed stable SHA and actual Project/native/CI admission; produces verified main plus every active dev disposition.

- [ ] Once Project access and a fresh native UI grant exist, perform bounded EN/RU/restart/emergency acceptance on immutable artifacts. Publish PR with required metadata and obtain exact-final-head hosted checks before merge.
- [ ] Normally integrate stable main and read actual SHA/tree/post-merge checks. Coordinate adoption into newly active branches with their owners; preserve frozen refs and amend #234's future baseline/resource contract explicitly.
- [ ] Verify final active refs, beta isolation and all acceptance evidence. Close #235/#236 and set/read actual Project Done only when their own criteria pass. Otherwise leave open with concrete blockers and preserved local candidate.

## Accepted local and native progress on 13 November

Tasks1-3 have named-revision baseline, conversion, regression, artifact, documentation and independent review/correction evidence in the linked report. The original local-preparation exception and later actual Project intake remain explicit; no historical before-implementation readback is invented. Native275 subsequently completed and independently accepted all16 original scenarios oncd5, with actual release276 and all cleanup limits retained. PR239 is normally integrated at5c280 with the exact tested tree.

Task4 remains incomplete: final PR237 HEAD review/CI, actual main integration/post-merge verification and active owner-controlled adoption are pending at this documentation revision. Preserve the JSON beta, eight frozen presentation refs, Root font branch and foreign primary-main WIP/index. Main-stage acceptance can unblock234 separately from broad236 closure.
