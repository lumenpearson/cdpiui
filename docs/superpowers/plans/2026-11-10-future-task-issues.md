# Future task issues implementation plan

> **For agentic workers:** Use `superpowers:executing-plans` to implement and verify this bounded policy change.

**Goal:** Record every future fork task with a detailed issue and actual Project fields, then close it immediately after scoped acceptance.

**Architecture:** Root agent instructions and a task form define the persistent operator workflow. The trusted contribution check validates a complete active issue contract. Agents verify actual Project data and semantic acceptance through API or the Codex browser.

**Tech stack:** Python standard library, unittest, GitHub issue forms and existing Actions metadata status.

**Specification:** [Issue #203](https://github.com/lumenpearson/cdpiui/issues/203), based on the completed [retrospective #202](https://github.com/lumenpearson/cdpiui/issues/202).

## Constraints and review focus

- Keep the canonical labels, private Project 7, G1–G5 milestones and protected-main integration process.
- Never execute PR code in the trusted metadata workflow or add credentials.
- A metadata validator cannot certify semantic acceptance or private Project fields with an unprivileged workflow token.
- Existing issues can receive an additive task contract. Do not duplicate a coherent task or erase its historical evidence.
- Raw merge or closure cannot certify a broad gate. Verified local stages retain their local scope.
- Re-read linked issue metadata before publishing a result; issue edits must cause a new check of affected open PRs.

## One deliverable: persistent intake and close-out policy

- [x] Reproduce acceptance of an incomplete linked issue with a failing regression.
- [ ] Add the contract validator, positive/negative fixtures and linked-issue freshness checks.
- [ ] Add root Codex/Claude instructions, the detailed task form, close-out procedure and document inventory entries.
- [ ] Run unit tests, document checks and whitespace checks. Obtain independent review of the final diff.
- [ ] Publish a reviewed PR linked to #203; verify exact-head checks and integrate through protection.
- [ ] Verify main source, the live task contract and actual Project fields. Record acceptance, close #203 as completed and set Done immediately.
