# Fork development pipeline implementation plan

> **For agentic workers:** Use superpowers:executing-plans to execute this plan. Use superpowers:requesting-code-review for independent review.

**Goal:** Configure a traceable development process for `lumenpearson/cdpiui` and publish its working documentation.

**Architecture:** GitHub Issues hold work and audit findings. Milestones hold G1–G5 exit criteria. One private GitHub Project holds delivery status, priority and risk. Discussions explain the fork changes and link to evidence. Repository workflows check code and contribution metadata.

**Tech stack:** GitHub Issues, Projects, Discussions, Actions, Git, Python 3 and PowerShell 7.

**Source specification:** The owner request dated 2026-11-10 and the prepared contract, milestones, backlog and labels in the previous session `01a1c049-c090-70c1-bcfe-d38e1e2704fe` (`.superpowers/sdd/2026-11-10-fork-governance`).

## Global constraints

- Use only `lumenpearson/cdpiui` for writes. Preserve the upstream fork relationship.
- Preserve other sessions, branches, uncommitted files and active reviews.
- Preserve G1–G5, seven delivery statuses, the 18-label taxonomy and nine planned acceptance tasks.
- Do not invent dates, reviewers, test results, severity scores, releases or completed work.
- Use English technical prose with ASD-STE100 writing principles. Keep Russian counterparts readable and consistent. Do not claim certified compliance.
- Preserve the main README and documents from Storik4pro. Make targeted factual and navigation improvements to those files.
- Do not publish secret values. Record credential revocation as an owner verification task when its state is unknown.
- An implementation in an unmerged branch is not a completed fix in `main`.
- Do not merge the ongoing product branches or publish a release as part of process setup.
- No paid service, new credential or expanded account access is needed for this plan.

## Review focus

1. Historical fixes must link to commits reachable from `main`; open findings must retain residual risks.
2. Metadata automation must not execute code from untrusted pull requests with write permissions.
3. Project automations must not treat an unmerged closed PR as completed delivery.
4. Documentation edits must respect upstream authorship and exclude legal texts from style rewriting.
5. Security check requirements must name real successful jobs and must not make maintenance impossible for a solo owner.

## Task 1: Baseline and plan review

- [x] Read live repository access, current PRs, local worktrees and previous milestone plan.
- [x] Create an isolated worktree from `origin/main` (`d72ad478bc05c6ab9e53f46da1a2b6c58690b202`).
- [x] Review this plan independently. Record corrections before external configuration.
- [ ] Save source inventory and a before/after configuration ledger without credentials.

## Task 2: Repository governance and delivery dashboard

- [x] Verify Issues, Discussions and Projects are enabled.
- [x] Create the 18 labels with descriptions. Preserve unrelated existing labels.
- [x] Create five milestones from the previous plan, with measurable exit criteria and no invented deadlines.
- [x] Create and link `CDPI UI — Delivery & Release Gates`, initially private.
- [x] Configure Inbox, Ready, In progress, Review, Verification, Blocked and Done.
- [x] Configure Priority P0–P3, Area, Risk, and native Milestone/Assignees/Linked pull requests fields.
- [x] Configure Delivery, Gate readiness and Blockers views. Add auto-add for this fork's issues and PRs.
- [x] Read back the linked project, view filters, custom fields and visible cards.

## Task 3: Work queue and audit traceability

- [x] Publish a delivery contract issue and the nine original acceptance tasks with criteria, dependencies, triage owner and risk.
- [x] Read the available original audit or record its absence. Extract each independent security finding, including FS-01–FS-12.
- [x] Give each security finding its own issue: source revision, affected boundary, reproduction procedure, cause, impact, fix, regression checks and remaining verification.
- [x] Distinguish historical reproduction, newly executed reproduction and a proposed procedure. Never claim an unexecuted PoC ran.
- [x] Link findings to exact commits/PRs. Close only those whose defined implementation acceptance is proven in `main`; keep release acceptance separate.
- [x] Add remaining functional requests and review debt as distinct queue entries when the available audit identifies them.
- [x] Assign each issue a milestone and one type/area label. Set project priority, risk and status. Explain blocked dependencies.
- [x] Pin the delivery contract, security index and release acceptance issue.
- [x] Complete metadata on existing PRs, preserve their technical content and link the audit issues.

## Task 4: Documentation and Discussions

- [x] Inventory tracked Markdown by origin and scope. Classify main README, upstream documents, fork documents and legal/notices.
- [x] Publish a documentation index, engineering process, security policy, style guide, release gates and audit index.
- [x] Align fork documentation to short, active technical English and consistent terms. Keep EN/RU navigation and facts consistent.
- [x] Improve upstream-created documents with targeted corrections and links. Preserve their structure and voice.
- [x] Organize Discussions by purpose; publish a pinned fork change index and evidence-based discussions for each change family.
- [x] Link Discussions to PRs, audit issues, documents and release gates. Use the same terminology throughout.

## Task 5: Automated contribution and release checks

- [x] Add structured bug, feature and audit issue forms, a PR template, CODEOWNERS and Dependabot configuration.
- [x] Add a metadata workflow and deterministic checker for PR title/body, issue links, type/area labels, assignee and milestone.
- [x] Test the checker with valid and invalid payloads, including hostile PR text. Use read-only permissions and environment inputs.
- [x] Keep existing code CI. Add checks only where they have a measured purpose; pin actions to verified full SHAs.
- [ ] Configure main branch protection after required checks exist: PRs, current-head CI, resolved conversations, no force push or deletion. Do not invent a second human reviewer.
- [x] Document independent review requirements and exceptions with evidence. Leave release publication as an explicit release decision.

## Task 6: Verification and delivery

- [ ] Run document link checks, structured configuration parsing, checker tests and `git diff --check`.
- [x] Obtain independent review and correct material findings.
- [x] Commit only this task's files with a complete message, push the isolated branch and create a fully described PR.
- [x] Apply labels, assignee, milestone and Project fields to the PR. Attach it to this chat.
- [ ] Verify Actions on the exact PR HEAD. Record unavailable checks and unresolved external settings truthfully.
- [ ] Read back all GitHub writes, compare issue/project counts and save dashboard screenshots.
- [ ] Release the browser UI queue. Deliver links, exact changes, checks and any remaining limitations.

## Self-review corrections

- Use a private Project as the prepared plan specifies; the repository itself stays public.
- Keep severity in the issue evidence, priority/risk in Project fields and stage in Milestone to avoid competing state sources.
- Treat dates as unset until estimated. An empty schedule with a reason is more accurate than a fabricated deadline.
- Separate code-fix acceptance from installed-product and release qualification.
- Existing immutable commit messages cannot carry labels or project fields. Put those fields on the linked issue/PR and define complete messages for future commits.
- Independent review found no Critical architecture blocker. Add a coverage matrix for every available audit ID and record the missing original report as an explicit completeness limit.
- Metadata validation must run against current API metadata and the current head. Handle label, assignment, milestone and body changes; provide a manual recovery run. Bootstrap without making its own first run mandatory.
- Check state transitions explicitly: accepted issue closure, reopening, merged PR closure and unmerged PR closure. Do not enable a raw-close-to-Done rule.
- Configure the dashboard and repository settings through the Codex browser. Verify branch rules and Discussions after saving.

## Execution checkpoint

The external metadata readback contains 99 unique content URLs: 89 issues and 10 PRs, including setup PR #111.
The audit subset has 78 findings. All Priority, Area, Risk and Status values match the publication plan.
The linked Project is number 7. Discussions #100–#110 explain the fork changes and delivery rules.
The current main history base is `7ead95f7756057f67cd113671a7f84cf422e8d32`.
Its file tree equals the recorded d72 baseline; product PR10 ancestry requires its own reconciliation.
The remaining activation and hosted verification results belong in [setup issue #11](https://github.com/lumenpearson/cdpiui/issues/11).
