# Delivery gates

The fork uses five milestones from the prepared development plan.
Milestones contain acceptance stages. Project Status contains the work state.
Priority and Risk are separate Project fields.
The labels describe work type and affected area.

## Milestones

| Gate | Scope | Required exit evidence |
|---|---|---|
| G1 | Defects and security boundaries | Audit inventory, reproducible findings, regressions, independent review, current-head CI and native boundary checks. No unresolved P0/P1 release blocker. |
| G2 | Architecture and state ownership | Explicit owners and lifecycle; preserved contracts; reentry and notification tests; native acceptance; integrated reviewed changes. |
| G3 | Functional acceptance | Integrated scenario matrix for GUI, tray, Store, components, import/export, add-ons, cancellation, errors and recovery. |
| G4 | Interface and documentation | EN/RU, keyboard, focus, accessible names and scaling; accurate instructions, working links and consistent terminology. |
| G5 | Release candidate | Exact candidate SHA, CI, signatures, artifact hashes, MSI/portable layout, installation, upgrade, rollback, uninstall and a recorded go/no-go decision. |

G2 can proceed alongside G1 in isolated worktrees.
G3 and G4 accept the integrated revision.
G5 depends on the exit evidence of G1–G4.
Due dates stay unset until scope and capacity are estimated.

## Project fields

| Field | Values and meaning |
|---|---|
| Priority | P0: active critical incident; P1: serious defect or mandatory acceptance blocker; P2: planned work; P3: optional improvement. |
| Risk | High: privileges, execution, installation or destructive writes; Medium: persisted user state or serialization; Low: local presentation without state changes. |
| Area | Security, Reliability, Architecture, Native QA, Integration, UX, Documentation, Release. |
| Milestone | One owning G1–G5 gate. A cross-gate task links to dependent issues. |
| Assignees | Contact for triage or the agreed implementation owner. |

Explain the impact behind a Priority or Risk value in the issue.
Use the same area in labels and the Project field.
An estimate, start date or target date can remain unset with a stated reason.
Do not use estimates as evidence of completed work.

## State transitions

| Status | Entry criteria | Next action |
|---|---|---|
| Inbox | New work needs triage, risk analysis or a complete contract. | Complete the issue before scheduling it. |
| Ready | Scope, criteria, dependencies and verification method are defined. | Assign implementation capacity. |
| In progress | An owner and an active branch or PR exist. | Complete the scoped change and checks. |
| Review | The PR is reviewable and required checks pass on its HEAD. | Obtain independent review and resolve findings. |
| Verification | Review is complete or an old implementation needs acceptance evidence. | Execute the remaining native, integrated or release checks. |
| Blocked | The issue states the blocker, unblock condition, next action and previous status. | Remove the blocker, then restore the previous working status. |
| Done | The issue criteria passed and the closing record names evidence and revision. | Retain traceability. Reopen on a verified regression. |

The board does not automatically turn raw issue closure into accepted work.
A closed unmerged PR must not become Done.
A merged PR records integration; it does not close a separate verification issue.
When an issue or PR reopens, review its status and remove a stale Done state.
Keep these transitions manual when native automation cannot check the evidence requirement.

## Views

- Delivery: board grouped by Status.
- Gate readiness: table grouped by Milestone, with Priority, Risk, Area and owner.
- Blockers: table filtered to `status:Blocked`.

The Project starts private and is linked to this fork.
The auto-add filter must include issues and PRs from `lumenpearson/cdpiui`.
Auto-add does not populate risk, priority or acceptance evidence.
Check the visible board as well as the item list after imports.
