"""Check local document paths and the published governance inventories."""

from pathlib import Path
import json
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[2]


def main():
    errors = []
    tracked = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=ROOT).decode().split("\0")
    documents = sorted({ROOT / name for name in tracked if name.endswith(".md") and (ROOT / name).is_file()})
    for path in documents:
        text = path.read_text(encoding="utf-8-sig")
        text = re.sub(r"(?ms)^\s*(`{3,}|~{3,}).*?^\s*\1\s*$", "", text)
        for target in re.findall(r"\[[^\]\n]+\]\(([^)\n]+)\)", text):
            target = target.strip().strip("<>")
            if urlsplit(target).scheme or target.startswith(("#", "/")):
                continue
            name = unquote(target.split("#", 1)[0].split("?", 1)[0])
            if name and not (path.parent / name).exists():
                errors.append(f"{path.relative_to(ROOT)}: missing relative target {name}")
    registry = ROOT / "docs/audit/findings.json"
    if registry.exists():
        data = json.loads(registry.read_text(encoding="utf-8"))
        rows = data["findings"]
        keys = [row["key"] for row in rows]
        numbers = [row["issue_number"] for row in rows]
        if len(keys) != len(set(keys)) or len(numbers) != len(set(numbers)):
            errors.append("The audit register contains duplicate finding IDs or issue numbers.")
        if data["finding_count"] != len(rows):
            errors.append("The audit register count does not match its rows.")
        for row in rows:
            if row["issue_url"] != f"https://github.com/lumenpearson/cdpiui/issues/{row['issue_number']}":
                errors.append(f"Finding {row['key']} links outside the expected repository.")
    inventory = ROOT / "docs/governance/document-inventory.json"
    if inventory.exists():
        data = json.loads(inventory.read_text(encoding="utf-8"))
        listed = [item["path"] for item in data["documents"]]
        if len(listed) != len(set(listed)):
            errors.append("The document inventory contains duplicate paths.")
        for path in documents:
            relative = path.relative_to(ROOT).as_posix()
            if relative not in listed:
                errors.append(f"Document inventory entry is missing: {relative}")
        for item in data["documents"]:
            if not (ROOT / item["path"]).is_file():
                errors.append(f"Document inventory path is missing: {item['path']}")
    for error in errors:
        print(f"ERROR: {error}")
    print(f"Checked {len(documents)} Markdown files and the available governance inventories; {len(errors)} errors.")
    return bool(errors)


if __name__ == "__main__":
    sys.exit(main())
