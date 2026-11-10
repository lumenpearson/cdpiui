"""Validate current GitHub metadata. Never import or run code from a pull request."""

import json
import os
import re
import sys
from urllib.error import HTTPError
from urllib.request import Request, urlopen


TYPES = {"bug", "security", "audit", "refactor", "verification", "docs", "release", "process"}
AREAS = {"security", "reliability", "architecture", "native-qa", "integration", "ux", "documentation", "release"}
SECTIONS = ("Problem", "Changes", "Verification", "Risk and recovery", "Related work")


def sections(body):
    """Read actual prose, excluding comments, fenced examples and block quotes."""
    body = re.sub(r"<!--[\s\S]*?-->", "", body or "")
    body = re.sub(r"(?ms)^\s*(`{3,}|~{3,}).*?^\s*\1\s*$", "", body)
    body = "\n".join(line for line in body.splitlines() if not line.lstrip().startswith(">"))
    matches = list(re.finditer(r"(?m)^##\s+(.+?)\s*$", body))
    return {m[1]: body[m.end(): matches[i + 1].start() if i + 1 < len(matches) else len(body)].strip()
            for i, m in enumerate(matches)}


def referenced_issues(body, repository):
    related = sections(body).get("Related work", "")
    related = re.sub(r"`+[^`]*`+", "", related)
    numbers = {int(n) for n in re.findall(r"(?<![\w/#])#([1-9]\d*)\b(?!\w)", related)}
    pattern = r"https://github\.com/" + re.escape(repository) + r"/issues/([1-9]\d*)\b(?![\w/])"
    numbers.update(int(n) for n in re.findall(pattern, related))
    return numbers


def validate(pr, known_issue_numbers, repository):
    if not isinstance(pr, dict):
        return ["The pull request payload must be an object."]
    errors = []
    title = pr.get("title") or ""
    if not re.fullmatch(r"(?:build|chore|ci|docs|feat|fix|perf|refactor|revert|test)(?:\([\w.-]+\))?!?: .+", title) or len(title) > 100:
        errors.append("Use a conventional title with at most 100 characters, for example: fix: preserve settings after cancellation.")
    content = sections(pr.get("body"))
    for name in SECTIONS:
        text = content.get(name, "")
        if len(text) < (1 if name == "Related work" else 12) or re.search(r"\b(?:TODO|TBD)\b|Describe your checks here", text, re.I):
            errors.append(f"Complete the {name!r} section with specific evidence or a justified limitation.")
    labels = {item.get("name", "") for item in pr.get("labels") or [] if isinstance(item, dict)}
    types = {label for label in labels if label.startswith("type:")}
    areas = {label for label in labels if label.startswith("area:")}
    if len(types) != 1 or not types <= {f"type:{name}" for name in TYPES}:
        errors.append("Set exactly one documented type: label.")
    if not areas or not areas <= {f"area:{name}" for name in AREAS}:
        errors.append("Set at least one documented area: label.")
    if not pr.get("assignees"):
        errors.append("Assign a maintainer or the agreed implementation owner.")
    milestone = pr.get("milestone") or {}
    if not isinstance(milestone, dict) or not re.match(r"^G[1-5] — ", milestone.get("title", "")):
        errors.append("Set the G1–G5 milestone that owns this work.")
    if not referenced_issues(pr.get("body"), repository) & known_issue_numbers:
        errors.append("Link an existing issue in this repository from the Related work section.")
    return errors


def required_metadata(pr):
    """Exclude unrelated repository, user and milestone counters from freshness checks."""
    milestone = pr.get("milestone") or {}
    return (pr.get("title"), pr.get("body"), pr.get("state"), pr.get("head", {}).get("sha"),
            frozenset(item.get("name") for item in pr.get("labels") or []),
            frozenset(item.get("login") for item in pr.get("assignees") or []),
            milestone.get("number"), milestone.get("title"))


def run_live():
    """Execute only from the trusted base branch on a GitHub-hosted runner."""
    repository = os.environ["GITHUB_REPOSITORY"]
    number = int(os.environ["PR_NUMBER"])
    if number < 1:
        raise ValueError("PR_NUMBER must be positive.")
    api = os.environ.get("GITHUB_API_URL", "https://api.github.com").rstrip("/")
    token = os.environ["GITHUB_TOKEN"]

    def request(path, data=None):
        payload = None if data is None else json.dumps(data).encode()
        req = Request(f"{api}/repos/{repository}/{path}", data=payload, headers={
            "Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28", "Content-Type": "application/json",
        })
        with urlopen(req, timeout=30) as response:
            return json.load(response)

    pr = request(f"pulls/{number}")
    if pr["state"] != "open":
        print("The pull request is closed. No delivery state is inferred.")
        return 0
    sha = pr["head"]["sha"]
    request(f"statuses/{sha}", {"state": "pending", "context": "Contribution metadata", "description": "Read current issue, label, owner and milestone data."})
    known = set()
    for issue_number in referenced_issues(pr.get("body"), repository):
        try:
            issue = request(f"issues/{issue_number}")
            if "pull_request" not in issue:
                known.add(issue_number)
        except HTTPError as error:
            if error.code != 404:
                raise
    errors = validate(pr, known, repository)
    latest = request(f"pulls/{number}")
    if required_metadata(pr) != required_metadata(latest):
        print("Metadata or HEAD changed during validation. The new event or a manual run must validate it.")
        return 1
    state = "failure" if errors else "success"
    run_url = f"{os.environ['GITHUB_SERVER_URL']}/{repository}/actions/runs/{os.environ['GITHUB_RUN_ID']}"
    request(f"statuses/{sha}", {"state": state, "context": "Contribution metadata",
                              "description": "Complete the required PR fields; see the workflow log." if errors else "Issue, description, labels, owner and milestone are valid.",
                              "target_url": run_url})
    for error in errors:
        print(f"ERROR: {error}")
    print(f"Metadata result: {state}; PR {number}; HEAD {sha}.")
    return 1 if errors else 0


if __name__ == "__main__":
    try:
        sys.exit(run_live())
    except HTTPError as error:
        print(f"GitHub API request failed: HTTP {error.code}. No credential or response body is printed.", file=sys.stderr)
        sys.exit(1)
