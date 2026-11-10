import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parents[1]))
from task_contract import SECTIONS, validate_issue
from task_fixture import valid_issue


class TaskContractTests(unittest.TestCase):
    def test_complete_intake_and_recorded_limits_pass(self):
        self.assertEqual([], validate_issue(valid_issue(), require_open=True))

    def test_missing_each_required_section_fails(self):
        for section in SECTIONS:
            issue = valid_issue()
            issue["body"] = issue["body"].replace("## " + section, "## Other material")
            with self.subTest(section=section):
                self.assertTrue(validate_issue(issue))

    def test_form_level_three_headings_pass(self):
        issue = valid_issue()
        issue["body"] = issue["body"].replace("## ", "### ")
        self.assertEqual([], validate_issue(issue))

    def test_quoted_fenced_or_comment_contract_is_not_evidence(self):
        for wrap in [lambda x: "```text\n" + x + "\n```", lambda x: "<!--\n" + x + "\n-->",
                     lambda x: "\n".join("> " + line for line in x.splitlines())]:
            issue = valid_issue()
            issue["body"] = wrap(issue["body"])
            self.assertTrue(validate_issue(issue))

    def test_metadata_and_declared_classification_must_agree(self):
        for field, value in [("labels", [{"name": "type:bug"}, {"name": "area:ux"}]),
                             ("milestone", {"title": "G1 — Defects and security boundaries"}),
                             ("assignees", [{"login": "another-owner"}])]:
            issue = valid_issue()
            issue[field] = value
            with self.subTest(field=field):
                self.assertTrue(validate_issue(issue))

    def test_invalid_and_duplicate_classification_fail(self):
        for before, after in [("Priority: P2", "Priority: urgent"), ("Risk: Low", "Risk: unknown"),
                              ("Area: Documentation", "Area: unknown"), ("Owner: @lumenpearson", "Owner: anyone"),
                              ("Project: https://github.com/users/lumenpearson/projects/7", "Project: https://example.invalid"),
                              ("Priority: P2", "Priority: P2\nPriority: P0")]:
            issue = valid_issue()
            issue["body"] = issue["body"].replace(before, after)
            with self.subTest(after=after):
                self.assertTrue(validate_issue(issue))

    def test_placeholders_and_vague_acceptance_fail(self):
        for before, after in [("The fork needs", "TODO: the fork needs"),
                              ("Record the requested result and its measurable acceptance conditions.", "Done")]:
            issue = valid_issue()
            issue["body"] = issue["body"].replace(before, after)
            self.assertTrue(validate_issue(issue))

    def test_duplicate_required_heading_fails(self):
        issue = valid_issue()
        issue["body"] += "\n## Scope and boundaries\nAn additional scope must not silently replace the first task scope."
        self.assertTrue(validate_issue(issue))

    def test_closed_task_only_passes_readback_mode(self):
        issue = valid_issue()
        issue["state"] = "closed"
        self.assertEqual([], validate_issue(issue))
        self.assertTrue(validate_issue(issue, require_open=True))

    def test_pull_request_and_invalid_payloads_fail(self):
        issue = copy.deepcopy(valid_issue())
        issue["pull_request"] = {}
        for value in [issue, None, [], "body"]:
            self.assertTrue(validate_issue(value))


if __name__ == "__main__":
    unittest.main()
