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

- [ ] Reference the actual tray project from a Windows-only xUnit project. Do not mock Font, Label, ModernButtonUserControl or their setters.
- [ ] Run each case on an STA test thread. Construct the control, assign literal `Exit` or `Выход`, then apply the literal style sequence.
- [ ] Cover false/false/false, true/true/true, true/false/false, false/true/false in both languages. Use true/false/true in both languages as a positive unequal-assignment control.
- [ ] After each assignment, verify the requested style, call Font.GetHeight with a bitmap Graphics, draw the caption into that private bitmap and require non-background pixels. Never capture the desktop or create a visible window.
- [ ] Under the reviewed RED grant, run the unchanged full managed baseline and these tests. Preserve actual assertion failures, positive controls, runtime identities, logs, TRX and process cleanup. Do not call a build failure RED.

## Task 2: Correct and verify the font lifetime

Files: `CDPIUI.TrayIcon/Controls/ModernButtonUserControl.cs`, the new tests, the Windows CI test command and relevant developer instructions.

- [ ] Only after the observed RED, correct the equal-style path without changing public signatures, caption text, glyphs or command callbacks. Preserve the current nominal ownership of the designer/private caption font.
- [ ] Check the new path against a shared reference to the unchanged selected font. An equal assignment must not dispose it or allocate a discarded live replacement.
- [ ] Under a new serial grant, pass all new font cases, the full existing managed suite and a normal Release/x64 tray build. Reuse no historical count as a fresh result.
- [ ] Add the Windows-only project to the Windows CI test and package-audit commands; preserve the current Linux-compatible suite.
- [ ] Record independent complete-change review, final HEAD checks and exact CI MSI/payload provenance. Freeze the owned worktree during the next native cohort.

## Task 3: Native and integrated acceptance

- [ ] Source RESW prepares a new immutable package from the reviewed font HEAD and its exact MSI; do not resume native265.
- [ ] Independently review the new package. Execute the declared EN/RU, readable tray captions, normal Quit and zero-process checks under a fresh exclusive grant. Capture GUI/tray PID and creation-time continuity around live language changes.
- [ ] Preserve failed observations, final payload/input hashes, normal Sandbox disposal, host preservation and monitor cleanup. Obtain independent retained evidence review.
- [ ] Integrate normally, identify actual final PR/main SHA and tree, and verify the tested product/test tree bridge plus required final HEAD and post-integration checks.
- [ ] Complete issue238 acceptance/closure and actual Project Done only after its contract passes. Keep #236 broader adoption and #234 integration gates separate.

## Initial state

Project intake was read back under267 and released268: existing item304164716, In progress / P1 / UX / Low, G4, lumenpearson and type:bug plus area:ux/area:native-qa. Native265 remains a STOP with zero complete accepted cases. The Font equality/disposal mechanism is a source-supported hypothesis; no regression test or corrective product code has executed yet.
