# Integrate the ConfigMaker and Welcome presentation owners

Owning task: [issue 234](https://github.com/lumenpearson/cdpiui/issues/234). Execution owner: chat `01a1c049-c090-70c1-bcfe-d38e1e2704fe`, branch `dev/presentation-owner-integration`, isolated worktree `C:/Users/lumen/.codex/worktrees/presentation-owner-integration/cdpiui`.

## Required result

Integrate the existing ConfigMaker status and Welcome migration presentation owners into accepted RESW main. Preserve the public view-model surface, stored state, notification order, sender identity, synchronous reentry, exception propagation and localization lookup order. The extracted owners keep narrow presentation responsibilities and typed ports. They must not acquire migration execution, dispatcher, navigation, persistence, file-picker or application lifetime responsibilities.

The accepted base is `d21167564afbf60a0c6ad5b5f559a464c1e261f6`, tree `7b80c9f4569f488e7fcece33f8982dbbc7db5c2c`. The frozen Welcome input is `5c7a9459589b6d8c18a794d675c29ac0ed285e54` from [PR 232](https://github.com/lumenpearson/cdpiui/pull/232). It contains ConfigMaker `b0f2c93d66000eb02cd963cf4421173980686e95` from [PR 230](https://github.com/lumenpearson/cdpiui/pull/230). Use a normal merge with both parents. Preserve all original branch heads and foreign worktrees.

## Composition contract

- Preserve accepted RESW authoring, all language and project changes, and the accepted Tray font correction from main. Do not restore the historical JSON authoring experiment.
- Retain all 130 explicit baseline test Compile items. Add exactly the three frozen links for `ConfigMakerStatusPresentation`, `MigrationComponentDisplayItem` and `WelcomeMigrationPresentation`; the composed set has 133 unique items. In particular, retain `LanguageSelectModel` and `StringToUpperConverter` from main.
- Preserve all existing document inventory records. Add the six frozen documents with their recorded introduction commits and authors. New integration records must acquire their actual introduction commit after it exists.
- The only product delta from the base is the five ConfigMaker/Welcome paths in the frozen change. No unrelated product correction, package update or dependency change is authorized by this composition.

## Verification and acceptance

First retain a fresh baseline, then compose the candidate and execute its own checks. Compare the current source and actual compiled surfaces against the retained baseline. Run the focused presentation/language checks, the complete managed suite, the Windows font suite and normal Release/x64 GUI build and publish. Verify source, loose, embedded and PRI resources from current RESW inputs.

Prepare a new bounded WinUI host for all 190 canonical groups and the 21 declared boundary cuts. Preserve the 37 raw case partials, regenerate the candidate bootstrap and binary pins, and keep the old failed CreateNew preparation as a negative control. Only the host creates the three literal input files. Execute one host and, after separate acceptance, one actual-close tail under fresh coordinator grants. An old fixture, successful compile or READY packet does not prove new runtime acceptance.

Obtain one fresh independent whole-product review, including the historical Welcome code authored by the coordinator. Required checks must pass on the final PR HEAD. Record the actual disposition of both original PRs, the integrated main commit and post-integration evidence. Complete all seven issue criteria before closure as `completed` and Project `Done`, with actual metadata readback in the same working turn.

## Boundaries and recovery

This task accepts the stated G2 composition. It does not accept the broad G3/G4/G5 matrix, real picker interaction, accessibility, an installed candidate, a release or the separate foreign-owner adoption task [236](https://github.com/lumenpearson/cdpiui/issues/236). Preserve failed attempts, original refs, retained baseline bytes and existing evidence. Stop dependent execution on a failed gate; investigate before any fresh qualified attempt. UI and heavy execution require the shared exclusive queue. Do not create another monitor.

See the [execution plan](../plans/2026-11-14-presentation-owner-integration.md) and [current verification report](../reports/2026-11-14-presentation-owner-integration.md).
