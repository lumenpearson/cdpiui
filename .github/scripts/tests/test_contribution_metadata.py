import copy
import importlib.util
import io
import json
from pathlib import Path
import sys
import unittest
from task_fixture import valid_issue
from unittest.mock import patch


sys.path.insert(0, str(Path(__file__).parents[1]))

MODULE = Path(__file__).parents[1] / "contribution_metadata.py"
spec = importlib.util.spec_from_file_location("contribution_metadata", MODULE)
metadata = importlib.util.module_from_spec(spec)
spec.loader.exec_module(metadata)


def valid_pr():
    return {
        "title": "ci: check contribution metadata",
        "body": """## Problem
Changes need an issue and a verifiable result.
## Changes
Add a metadata check that reads current repository data.
## Verification
Run the checker tests and inspect the current-head status.
## Risk and recovery
Revert this workflow if it blocks valid contributions.
## Related work
Refs #42.
""",
        "labels": [{"name": "type:process"}, {"name": "area:documentation"}],
        "assignees": [{"login": "lumenpearson"}],
        "milestone": {"title": "G4 — Interface and documentation"},
    }


class MetadataTests(unittest.TestCase):
    def errors(self, pr):
        return metadata.validate(pr, {42: valid_issue()}, "lumenpearson/cdpiui")

    def test_complete_current_metadata_passes(self):
        self.assertEqual([], self.errors(valid_pr()))

    def test_missing_each_required_field_fails(self):
        for field in ["title", "body", "labels", "assignees", "milestone"]:
            with self.subTest(field=field):
                pr = valid_pr()
                pr[field] = None
                self.assertTrue(self.errors(pr))

    def test_unrelated_and_nonexistent_issue_references_fail(self):
        for reference in ["https://github.com/other/repo/issues/42", "#999", "#42fake", "issue 42", "https://github.com/lumenpearson/cdpiui/pull/42"]:
            with self.subTest(reference=reference):
                pr = valid_pr()
                pr["body"] = pr["body"].replace("Refs #42.", reference)
                self.assertTrue(self.errors(pr))

    def test_repository_issue_url_passes(self):
        pr = valid_pr()
        pr["body"] = pr["body"].replace("#42", "https://github.com/lumenpearson/cdpiui/issues/42")
        self.assertEqual([], self.errors(pr))

    def test_reference_only_in_fenced_example_does_not_pass(self):
        pr = valid_pr()
        pr["body"] = pr["body"].replace("Refs #42.", "```text\nRefs #42\n```")
        self.assertTrue(self.errors(pr))

    def test_reference_only_in_inline_example_does_not_pass(self):
        pr = valid_pr()
        pr["body"] = pr["body"].replace("Refs #42.", "For example, `Refs #42`.")
        self.assertTrue(self.errors(pr))

    def test_blank_or_placeholder_section_fails(self):
        for value in ["", "TODO", "Describe your checks here.", "- [ ] TODO"]:
            with self.subTest(value=value):
                pr = valid_pr()
                pr["body"] = pr["body"].replace("Run the checker tests and inspect the current-head status.", value)
                self.assertTrue(self.errors(pr))

    def test_one_type_and_at_least_one_area_are_required(self):
        for labels in [[], [{"name": "bug"}], [{"name": "type:bug"}], [{"name": "area:security"}], [{"name": "type:bug"}, {"name": "type:security"}, {"name": "area:security"}]]:
            with self.subTest(labels=labels):
                pr = valid_pr()
                pr["labels"] = labels
                self.assertTrue(self.errors(pr))

    def test_unknown_taxonomy_values_fail(self):
        pr = valid_pr()
        pr["labels"] = [{"name": "type:invented"}, {"name": "area:invented"}]
        self.assertTrue(self.errors(pr))

    def test_metadata_correction_and_removal_are_rechecked(self):
        pr = valid_pr()
        pr["milestone"] = None
        self.assertTrue(self.errors(pr))
        pr["milestone"] = valid_pr()["milestone"]
        self.assertEqual([], self.errors(pr))
        pr["assignees"] = []
        self.assertTrue(self.errors(pr))

    def test_hostile_text_is_only_data(self):
        pr = copy.deepcopy(valid_pr())
        pr["title"] = "fix: handle $(echo attack) and `echo attack` as text"
        pr["body"] += "\n<script>never_execute()</script>"
        self.assertEqual([], self.errors(pr))

    def test_milestone_must_be_a_gate(self):
        pr = valid_pr()
        pr["milestone"] = {"title": "Unscoped work"}
        self.assertTrue(self.errors(pr))

    def test_non_object_payload_fails(self):
        for pr in [None, [], "text"]:
            with self.subTest(pr=pr):
                self.assertTrue(self.errors(pr))


class LiveStatusTests(unittest.TestCase):
    def run_with_latest(self, change, issue_change=None, latest_issue_change=None):
        pr = valid_pr()
        pr.update(state="open", head={"sha": "a" * 40, "repo": {"pushed_at": "2026-11-10T12:00:00Z"}})
        latest = copy.deepcopy(pr)
        change(latest)
        issue = valid_issue()
        if issue_change:
            issue_change(issue)
        latest_issue = copy.deepcopy(issue)
        if latest_issue_change:
            latest_issue_change(latest_issue)
        reads = iter([pr, issue, latest, latest_issue])
        writes = []

        def request(req, timeout):
            self.assertEqual(timeout, 30)
            if req.data:
                writes.append((req.full_url, json.loads(req.data)))
                value = {}
            else:
                value = next(reads)
            return io.BytesIO(json.dumps(value).encode())

        env = {"GITHUB_REPOSITORY": "lumenpearson/cdpiui", "PR_NUMBER": "10",
               "GITHUB_TOKEN": "unit-test-placeholder", "GITHUB_SERVER_URL": "https://github.com",
               "GITHUB_RUN_ID": "123"}
        with patch.dict(metadata.os.environ, env), patch.object(metadata, "urlopen", side_effect=request):
            result = metadata.run_live()
        return result, writes

    def test_unrelated_repository_activity_does_not_leave_status_pending(self):
        result, writes = self.run_with_latest(lambda pr: pr["head"]["repo"].update(pushed_at="2026-11-10T12:00:01Z"))
        self.assertEqual(0, result)
        self.assertEqual(["pending", "success"], [data["state"] for _, data in writes])
        self.assertTrue(all(url.endswith("/statuses/" + "a" * 40) for url, _ in writes))

    def test_incomplete_linked_issue_publishes_failure(self):
        result, writes = self.run_with_latest(lambda pr: None, lambda issue: issue.update(body="Only an incomplete note."))
        self.assertEqual(1, result)
        self.assertEqual("failure", writes[-1][1]["state"])

    def test_head_change_does_not_publish_success_for_stale_validation(self):
        result, writes = self.run_with_latest(lambda pr: pr["head"].update(sha="b" * 40))
        self.assertEqual(1, result)
        self.assertEqual(["pending"], [data["state"] for _, data in writes])

    def test_irrelevant_embedded_metadata_does_not_leave_status_pending(self):
        changes = [lambda pr: pr["milestone"].update(open_issues=9, closed_issues=6),
                   lambda pr: pr["labels"][0].update(color="ffffff", description="New description"),
                   lambda pr: pr["assignees"][0].update(avatar_url="https://example.invalid/avatar")]
        for change in changes:
            with self.subTest(change=change):
                result, writes = self.run_with_latest(change)
                self.assertEqual(0, result)
                self.assertEqual(["pending", "success"], [data["state"] for _, data in writes])

    def test_linked_issue_change_does_not_publish_stale_success(self):
        result, writes = self.run_with_latest(lambda pr: None, latest_issue_change=lambda issue: issue.update(body="Changed task contract."))
        self.assertEqual(1, result)
        self.assertEqual(["pending"], [data["state"] for _, data in writes])

    def test_required_metadata_change_does_not_publish_stale_success(self):
        result, writes = self.run_with_latest(lambda pr: pr.update(assignees=[]))
        self.assertEqual(1, result)
        self.assertEqual(["pending"], [data["state"] for _, data in writes])


class IssueEventTests(unittest.TestCase):
    def test_all_open_pr_pages_are_checked_for_the_edited_issue(self):
        pages = []

        def request(path):
            pages.append(path)
            if path.endswith("page=1"):
                return [{"number": n, "body": "## Related work\nRefs #999."} for n in range(1, 100)] + [
                    {"number": 100, "body": "## Related work\nRefs #42."}]
            return [{"number": 101, "body": "## Related work\nhttps://github.com/lumenpearson/cdpiui/issues/42"},
                    {"number": 102, "body": "## Related work\n`Refs #42`"},
                    {"number": 103, "body": "## Related work\nhttps://github.com/other/repo/issues/42"}]

        self.assertEqual([100, 101], metadata.affected_pull_requests(request, "lumenpearson/cdpiui", 42))
        self.assertEqual(2, len(pages))

    def test_unrelated_issue_event_does_not_write_a_status(self):
        env = {"GITHUB_REPOSITORY": "lumenpearson/cdpiui", "GITHUB_TOKEN": "unit-test-placeholder",
               "PR_NUMBER": "", "ISSUE_NUMBER": "42"}
        with patch.dict(metadata.os.environ, env), patch.object(metadata, "urlopen", return_value=io.BytesIO(b"[]")) as urlopen:
            self.assertEqual(0, metadata.run_live())
        self.assertEqual(1, urlopen.call_count)
        self.assertIsNone(urlopen.call_args.args[0].data)

    def test_related_issue_event_revalidates_the_affected_pr(self):
        env = {"GITHUB_REPOSITORY": "lumenpearson/cdpiui", "GITHUB_TOKEN": "unit-test-placeholder",
               "PR_NUMBER": "", "ISSUE_NUMBER": "42"}
        page = [{"number": 10, "body": "## Related work\nRefs #42."}]
        with patch.dict(metadata.os.environ, env), patch.object(metadata, "urlopen", return_value=io.BytesIO(json.dumps(page).encode())), \
                patch.object(metadata, "check_pull_request", return_value=1) as check:
            self.assertEqual(1, metadata.run_live())
        check.assert_called_once()
        self.assertEqual(("lumenpearson/cdpiui", 10), check.call_args.args[1:])


if __name__ == "__main__":
    unittest.main()
