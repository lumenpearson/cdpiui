# Tray font lifetime implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task. Preserve each failed result and verify the named revision before acceptance.

**Goal:** Keep the tray caption font usable after equal and repeated Bold assignments.

**Architecture:** Preserve the existing public control and its command events. Test the real Windows Forms control in a separate Windows test project; keep the existing cross-platform managed test project unchanged. Make the smallest correction supported by a real native-backed font reproduction.

**Tech stack:** C#, .NET 8, Windows Forms, System.Drawing.Common 8.0.31 and existing centrally managed xUnit packages.

**Spec:** [Issue #238](https://github.com/lumenpearson/cdpiui/issues/238).

## Global constraints

- Root owns `dev/tray-font-lifetime` in `C:/Users/lumen/.codex/worktrees/tray-font-lifetime/cdpiui`, created from exact `ffae24346fa80ed5a6fc4a5e2ae1de9c000aee21`.
- This base is the reviewed, managed-tested RESW preparation. Its installed acceptance is stopped, not accepted. Current accepted main remains `fe29acfc4238228da3a6aebb98fff4345c3da106`.
- Preserve all original worktrees, indexes, published frozen heads, failed native265, MSI inputs and the frozen JSON beta.
- Source RESW retains its worktree and PR #237. Publish only the font delta in a stacked PR into `dev/resw-localization`; record the final product/test tree and main integration separately.
- SDK and native-backed tests need a fresh serial resource grant. No visible window, desktop input, screenshot capture, product entry point, tray service, registry/settings write, host installation or external process termination is part of the regression test.
- Record loaded .NET, Windows Forms and System.Drawing assembly identities. Use the existing task-local .NET 8.0.33 desktop runtime.

## Review focus

- The initial designer Regular font followed by `Bold = false` must remain drawable.
- Repeated true/false assignments must preserve the requested style and a usable selected font.
- Genuine style transitions must still work; avoid a test that rejects all assignments.
- A cached Style or Equals result does not establish font health. Call native-backed height and drawing operations on the selected font.
- Keep font ownership, caption text, glyph behavior and click ordering in scope. Do not conceal a failed paint or change tray commands.

## Task 1: Reproduce the real control defect

Files: new `CDPIUI.TrayIcon.Tests/CDPIUI.TrayIcon.Tests.csproj` and `ModernButtonFontLifetimeTests.cs`; this plan and the owning issue record.

- [x] Reference the actual tray project from a Windows-only xUnit project. Do not mock Font, Label, ModernButtonUserControl or their setters.
- [x] Run each case on an STA test thread. Construct the control, assign literal `Exit` or `Выход`, then apply the literal style sequence.
- [x] Cover false/false/false, true/true/true, true/false/false, false/true/false in both languages. Use true/false/true in both languages as a positive unequal-assignment control.
- [x] After each assignment, verify the requested style, call Font.GetHeight with a bitmap Graphics, draw the caption into that private bitmap and require non-background pixels. Never capture the desktop or create a visible window.
- [x] Under the reviewed RED grant, run the unchanged full managed baseline and these tests. Preserve actual assertion failures, positive controls, runtime identities, logs, TRX and process cleanup. Do not call a build failure RED.

## Task 2: Correct and verify the font lifetime

Files: `CDPIUI.TrayIcon/Controls/ModernButtonUserControl.cs`, the new tests, the Windows CI test command and relevant developer instructions.

- [x] Only after the observed RED, correct the equal-style path without changing public signatures, caption text, glyphs or command callbacks. Preserve the current nominal ownership of the designer/private caption font.
- [x] Check the new path against a shared reference to the unchanged selected font. An equal assignment must not dispose it or allocate a discarded live replacement.
- [x] Under a new serial grant, pass all new font cases, the full existing managed suite and a normal Release/x64 tray build. Reuse no historical count as a fresh result.
- [x] Add the Windows-only project to the Windows CI test and package-audit commands; preserve the current Linux-compatible suite.
- [ ] Record independent complete-change review, final HEAD checks and exact CI MSI/payload provenance. Freeze the owned worktree during the next native cohort.

## Task 3: Native and integrated acceptance

- [ ] Source RESW prepares a new immutable package from the reviewed font HEAD and its exact MSI; do not resume native265.
- [ ] Independently review the new package. Execute the declared EN/RU, readable tray captions, normal Quit and zero-process checks under a fresh exclusive grant. Capture GUI/tray PID and creation-time continuity around live language changes.
- [ ] Preserve failed observations, final payload/input hashes, normal Sandbox disposal, host preservation and monitor cleanup. Obtain independent retained evidence review.
- [ ] Integrate normally, identify actual final PR/main SHA and tree, and verify the tested product/test tree bridge plus required final HEAD and post-integration checks.
- [ ] Complete issue238 acceptance/closure and actual Project Done only after its contract passes. Keep #236 broader adoption and #234 integration gates separate.

## Initial state

Project intake was read back under267 and released268: existing item304164716, In progress / P1 / UX / Low, G4, lumenpearson and type:bug plus area:ux/area:native-qa. Native265 remains a STOP with zero complete accepted cases. Before execution, the Font equality/disposal mechanism was a source-supported hypothesis.

## Actual RED evidence

Test-only revision `541d32fd135f72861ad2ded001a5e50bf48d07f2` ran under fresh269 and qualified release270. The full unchanged managed suite passed2600/2600, zero failed or skipped, in49.16seconds including build. The real-control font suite executed10 cases: eight expected assertion failures and two unequal-style positive passes, zero execution errors or skipped cases. Each failure contains `ArgumentException` from the selected `Font.GetHeight(Graphics)`; this was not a compile or restore failure. Its command exited1; the wrapper exited0 only because it verified the declared RED and positive controls.

Actual loaded runtime: .NET8.0.33, Windows Forms and System.Drawing.Common file version8.0.3326.52211, both loaded from the task-local WindowsDesktop8.0.33 framework. The NuGet8.0.31 file was a preparation pin, not the assembly selected by this test host. Its role must not be substituted for the actual loaded identity. Runtime path/version output and post-execution hashes are retained separately from the three prelaunch pins.

Baseline TRX SHA256: `3e875a1ed8fd35c0cb7046853924f12d4b1f371b845e0e55762dde1ebf1e039d`. Font RED TRX: `9fd9ccbd5a9f96fd764b56b428bbdfc5433f64dfd041d98b1fc99a243227cb42`. The immutable local manifest pins108 retained files, including nine used compiled files archived before any GREEN overwrite; SHA256 `4df785b202b79bb1fbd12e106ccd377bc97b2120069307ceaf36cb0e5ecee4f6`. Root freshly qualified absence of27 recorded exact process identities and the SDK/test process-name set. Sampling can miss short-lived descendants; no global process-absence claim is made. Actual release270 SHA256: `bc23ed2817ea223fd98a5824a6019affb2a3c12b8c34b4b88ffa59a3e3031316`.

The local font-use defect is reproduced. This result does not prove visible tray paint, the exact caught exception in native265, RESW16 acceptance or main integration.

## Actual local GREEN evidence

Root prepared the minimal guard only after RED: update the existing Bold field as before, then return when the selected font already has the requested style. That branch does not assign, allocate or dispose a font, so the selected font and any reference to it remain valid. Genuine style changes retain the existing replacement/disposal path. Captions, glyphs and click handlers are unchanged.

The exact uncommitted six-file candidate was frozen on base541d with binary patch SHA256 `69a6415241ff4a7bea8cbf344d24d511d868b10e54275920466a9f1a238bef53`, complete physical source/runtime hashes and a Git-status hash. This permitted tests before committing corrective product code. Under fresh271, all10 font cases passed, the complete existing suite passed2600/2600 and normal Release/x64 Tray build exited0 with zero warnings/errors. Actual step durations were7.41,15.46 and1.60seconds. Loaded framework/Windows Forms/Drawing remained .NET8.0.33 / file8.0.3326.52211.

Font GREEN TRX SHA256: `b0c698c4f7000dded1a468eb70b170eb8cd2ea03f33b5809ba7b5a48ef96e1e6`. Candidate managed TRX: `bd84fdb856fc6bb2ac4db51b973338d7b69a499457360992785f6b4cd0300d55`. The immutable local manifest pins54 retained files, including nine used compiled files; SHA256 `5c153edb1afcc30493dbf29214cb50cb3f695b35f5992c7738484afc707582e3`. Root verified all source/runtime inputs and the unchanged named patch, fresh absence of15 recorded exact process identities and the SDK/test-name set, then released272. Release SHA256: `cdcfc010637fc6fa84301d78bdc71af6a6eb48c05b7f1b074151e173b0718b8d`.

These are local working-candidate results. They are not a final committed-HEAD CI result, visible native tray proof, RESW16 or main acceptance. Independent complete-change review, final HEAD checks, exact MSI provenance and the separate native/integration gates remain required. The existing cross-platform test project remains unchanged; Windows Forms tests run explicitly in the Windows CI library job and through the documented command.
