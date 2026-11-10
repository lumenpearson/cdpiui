# Governance configuration record

This directory records the development process configured for the fork on 2026-11-10.
The source of the five-gate plan is the previous preparation session.
Its nine core tasks and 18 canonical labels remain the basis of this setup.

## Canonical GitHub objects

- Repository: [lumenpearson/cdpiui](https://github.com/lumenpearson/cdpiui).
- Private Project: [CDPI UI — Delivery & Release Gates](https://github.com/users/lumenpearson/projects/7).
- Delivery contract: [issue #11](https://github.com/lumenpearson/cdpiui/issues/11).
- Core acceptance tasks: issues [#12](https://github.com/lumenpearson/cdpiui/issues/12)–[#20](https://github.com/lumenpearson/cdpiui/issues/20).
- Security index: [issue #21](https://github.com/lumenpearson/cdpiui/issues/21).
- Milestones: [G1](https://github.com/lumenpearson/cdpiui/milestone/1), [G2](https://github.com/lumenpearson/cdpiui/milestone/2), [G3](https://github.com/lumenpearson/cdpiui/milestone/3), [G4](https://github.com/lumenpearson/cdpiui/milestone/4), [G5](https://github.com/lumenpearson/cdpiui/milestone/5).

The [document inventory](document-inventory.json) records original authorship and style exceptions.
The [audit register](../audit/README.md) records finding identity, implementation links and acceptance limits.

## Labels

Canonical types: bug, security, audit, refactor, verification, docs, release and process, each with the `type:` prefix.
Canonical areas: security, reliability, architecture, native-qa, integration, ux, documentation and release, each with the `area:` prefix.
The two input labels are `needs:decision` and `needs:reproduction`.

The ten pre-existing generic labels are retained as historical aliases.
Do not apply them to new delivery work.
Keep `enhancement` for proposal intake until triage selects the canonical work type.
This is an explicit preservation rule, not a second canonical taxonomy.

## Project state rules

Views: Delivery, Gate readiness and Blockers.
Fields: seven Status options, Priority P0–P3, eight Area options and Risk High/Medium/Low.
Use native Milestone, Assignees and Linked pull requests fields.
The milestone is the single source for the G1–G5 stage; there is no duplicate Gate field.

Auto-add is scoped to this fork and `is:issue,pr`.
New and reopened items enter Inbox.
Raw closure, PR linkage and PR merge must not automatically certify Done.
Issue closure and blocked-state restoration require the evidence rules in [delivery gates](../delivery-gates.md).

## Verification record

The setup is verified through current GitHub metadata, saved browser settings and exported Project rows.
Visible cards must agree with the export. Successful create responses alone are insufficient.
The final configuration snapshot distinguishes active external settings from repository changes awaiting integration.

No release date, estimate, human reviewer or test execution is invented to fill an optional field.
Historic fixes and active unmerged work retain their actual dispositions.

## Future task intake and close-out

Follow [the detailed task lifecycle](../task-lifecycle.md) for every future fork task.
Create or reuse its detailed issue before implementation and verify actual Project fields.
After scoped acceptance, post the closing evidence, close as completed and set Done immediately.
Codex and Claude use the root agent instructions. The contribution check validates an active owning task contract.
