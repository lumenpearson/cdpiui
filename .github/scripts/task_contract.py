"""Validate task records as data; acceptance and Project readback remain operator checks."""

import re


PROJECT = "https://github.com/users/lumenpearson/projects/7"
TYPES = {"bug", "security", "audit", "refactor", "verification", "docs", "release", "process"}
AREAS = {"Security": "security", "Reliability": "reliability", "Architecture": "architecture",
         "Native QA": "native-qa", "Integration": "integration", "UX": "ux",
         "Documentation": "documentation", "Release": "release"}
SECTIONS = ("Context and expected result", "Scope and boundaries", "Acceptance criteria",
            "Verification plan", "Risk and recovery", "Dependencies and documentation",
            "Project classification", "Progress and evidence")


def prose(body):
    body = re.sub(r"<!--[\s\S]*?-->", "", body or "")
    body = re.sub(r"(?ms)^\s*(`{3,}|~{3,}).*?^\s*\1\s*$", "", body)
    return "\n".join(line for line in body.splitlines() if not line.lstrip().startswith(">"))


def validate_issue(issue, *, require_open=False):
    if not isinstance(issue, dict) or "pull_request" in issue:
        return ["The task must be a repository issue, not a pull request."]
    errors = []
    if require_open and issue.get("state") != "open":
        errors.append("Keep the owning task open until its scoped acceptance is complete.")
    body = prose(issue.get("body"))
    headings = list(re.finditer(r"(?m)^#{2,3}\s+(.+?)\s*$", body))
    content = {}
    for index, heading in enumerate(headings):
        name = heading[1]
        if name in SECTIONS and name in content:
            errors.append(f"The task repeats the {name!r} section.")
        content[name] = body[heading.end():headings[index + 1].start() if index + 1 < len(headings) else len(body)].strip()
    for name in SECTIONS:
        text = content.get(name, "")
        if len(text) < 32 or re.search(r"\b(?:TODO|TBD)\b", re.sub(r"`[^`]*`", "", text), re.I):
            errors.append(f"Complete task section {name!r} with case-specific information.")
    if len(body.split()) < 180:
        errors.append("Provide at least 180 words of task context, scope, evidence and classification.")
    criteria = re.findall(r"(?m)^\s*- \[[ xX]\]\s+(.+)$", content.get("Acceptance criteria", ""))
    if len(criteria) < 2 or any(len(item.strip()) < 12 for item in criteria):
        errors.append("Give at least two concrete acceptance checklist items.")
    values = {}
    classification = content.get("Project classification", "")
    for field in ("Type", "Area", "Priority", "Risk", "Gate", "Owner", "Project"):
        matches = re.findall(r"(?m)^\s*(?:-\s+)?" + field + r":\s*(\S[^\n]*)$", classification)
        if len(matches) != 1:
            errors.append(f"Set exactly one {field} line in Project classification.")
        values[field] = matches[0].strip() if len(matches) == 1 else ""
    labels = {row.get("name") for row in issue.get("labels") or [] if isinstance(row, dict)}
    type_labels = {label for label in labels if isinstance(label, str) and label.startswith("type:")}
    area_labels = {label for label in labels if isinstance(label, str) and label.startswith("area:")}
    if values["Type"] not in {"type:" + name for name in TYPES} or type_labels != {values["Type"]}:
        errors.append("The task Type must match exactly one canonical type: label.")
    if values["Area"] not in AREAS or "area:" + AREAS.get(values["Area"], "") not in area_labels or not area_labels <= {"area:" + name for name in AREAS.values()}:
        errors.append("The task Area must agree with canonical area: labels.")
    if values["Priority"] not in {"P0", "P1", "P2", "P3"}:
        errors.append("Set task Priority to P0, P1, P2 or P3.")
    if values["Risk"] not in {"High", "Medium", "Low"}:
        errors.append("Set task Risk to High, Medium or Low.")
    milestone = issue.get("milestone") or {}
    if values["Gate"] not in {"G1", "G2", "G3", "G4", "G5"} or not milestone.get("title", "").startswith(values["Gate"] + " — "):
        errors.append("The task Gate must match its actual G1–G5 milestone.")
    owners = {row.get("login", "").casefold() for row in issue.get("assignees") or [] if isinstance(row, dict)}
    if not re.fullmatch(r"@[A-Za-z0-9-]+", values["Owner"]) or values["Owner"].lstrip("@").casefold() not in owners:
        errors.append("The task Owner must name an actual issue assignee.")
    if values["Project"] != PROJECT:
        errors.append("Link the canonical delivery Project 7 in task classification.")
    return errors
