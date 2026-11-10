def valid_issue():
    return {
        "number": 42, "state": "open", "title": "Record verified task delivery",
        "labels": [{"name": "type:process"}, {"name": "area:documentation"}],
        "assignees": [{"login": "lumenpearson"}],
        "milestone": {"number": 4, "title": "G4 — Interface and documentation"},
        "body": """## Context and expected result
The fork needs a durable task record before implementation starts. A maintainer must be able to understand the requested result without reading an earlier conversation. The issue records the current limitation and the user scenario that makes the change necessary.
## Scope and boundaries
This task changes the contribution process and its documentation. It preserves application behavior, stored settings and release acceptance. Other findings keep their own criteria. The linked implementation branch carries only this policy change and its focused tests.
## Acceptance criteria
- [ ] Record the requested result and its measurable acceptance conditions.
- [ ] Verify the delivered change and retain its case-specific evidence.
## Verification plan
Run the focused positive and negative policy tests. Check repository document links and inspect the final diff independently. Read the actual issue metadata and private delivery project fields before acceptance. Historical results must retain their tested revision.
## Risk and recovery
The change has Low risk because it affects documentation and contribution metadata. Revert the policy commit if it blocks valid contributions. Preserve the task history and record the corrective follow-up rather than silently weakening acceptance.
## Dependencies and documentation
The engineering process and delivery gates define the canonical taxonomy. The task depends on the existing private delivery project. No new credential, account or secret is required. Additional integration work remains separate when its acceptance is incomplete.
## Project classification
Type: type:process
Area: Documentation
Priority: P2
Risk: Low
Gate: G4
Owner: @lumenpearson
Project: https://github.com/users/lumenpearson/projects/7
## Progress and evidence
The task is ready for implementation. Verification commands, exact revisions, review results and remaining limits will be added when execution produces them. This intake record does not claim that tests or native scenarios have already run.
""",
    }
