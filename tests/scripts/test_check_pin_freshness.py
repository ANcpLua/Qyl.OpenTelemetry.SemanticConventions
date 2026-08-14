from __future__ import annotations

import contextlib
import http.client
import importlib.util
import io
import sys
import unittest
from pathlib import Path
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = (
    REPO_ROOT
    / "src"
    / "Qyl.Telemetry.SemanticConventions.SourceGeneration"
    / "scripts"
    / "check_pin_freshness.py"
)
SPEC = importlib.util.spec_from_file_location("check_pin_freshness", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"could not load {SCRIPT}")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)


class ReleasePinTests(unittest.TestCase):
    def test_equal_release_is_current(self) -> None:
        with mock.patch.object(
            CHECKER,
            "github_json",
            return_value={"tag_name": "v1.43.0", "html_url": "https://example.test/release"},
        ):
            current, lines = CHECKER.check_release_pin("core", "owner/repo", "1.43.0")

        self.assertTrue(current)
        self.assertIn("current", lines[0])

    def test_different_release_is_stale(self) -> None:
        with mock.patch.object(
            CHECKER,
            "github_json",
            return_value={"tag_name": "v1.44.0", "html_url": "https://example.test/release"},
        ):
            current, lines = CHECKER.check_release_pin("core", "owner/repo", "1.43.0")

        self.assertFalse(current)
        self.assertIn("latest release is `v1.44.0`", lines[0])

    def test_missing_release_tag_is_unknown(self) -> None:
        with mock.patch.object(CHECKER, "github_json", return_value={}):
            with self.assertRaises(CHECKER.FreshnessUnknown):
                CHECKER.check_release_pin("core", "owner/repo", "1.43.0")


class BranchPinTests(unittest.TestCase):
    def check(self, response: dict) -> tuple[bool, list[str]]:
        with mock.patch.object(CHECKER, "github_json", return_value=response):
            return CHECKER.check_branch_pin("genai", "owner/repo", "abcdef123456", "main")

    def test_identical_commit_is_current(self) -> None:
        current, lines = self.check(
            {"status": "identical", "ahead_by": 0, "behind_by": 0, "html_url": "https://example.test"}
        )

        self.assertTrue(current)
        self.assertIn("the head of `main`", lines[0])

    def test_branch_ahead_marks_pin_stale_and_lists_commits(self) -> None:
        current, lines = self.check(
            {
                "status": "ahead",
                "ahead_by": 1,
                "behind_by": 0,
                "html_url": "https://example.test",
                "commits": [{"sha": "123456789", "commit": {"message": "Registry change\n\nDetails"}}],
            }
        )

        self.assertFalse(current)
        self.assertIn("1 commit(s) behind", lines[0])
        self.assertIn("`1234567` Registry change", lines[2])

    def test_branch_behind_pin_is_not_reported_current(self) -> None:
        current, lines = self.check(
            {"status": "behind", "ahead_by": 0, "behind_by": 2, "html_url": "https://example.test"}
        )

        self.assertFalse(current)
        self.assertIn("behind the pin", lines[0])
        self.assertIn("force-push", lines[2])

    def test_diverged_pin_is_stale(self) -> None:
        current, lines = self.check(
            {"status": "diverged", "ahead_by": 3, "behind_by": 2, "html_url": "https://example.test"}
        )

        self.assertFalse(current)
        self.assertIn("diverged", lines[0])

    def test_missing_distance_is_unknown_instead_of_current(self) -> None:
        with self.assertRaises(CHECKER.FreshnessUnknown):
            self.check({"status": "ahead", "behind_by": 0, "html_url": "https://example.test"})

    def test_unknown_status_is_unknown(self) -> None:
        with self.assertRaises(CHECKER.FreshnessUnknown):
            self.check({"status": "surprising", "ahead_by": 0, "behind_by": 0})

    def test_inconsistent_status_distances_are_unknown(self) -> None:
        with self.assertRaises(CHECKER.FreshnessUnknown):
            self.check({"status": "identical", "ahead_by": 1, "behind_by": 0})

    def test_malformed_commit_is_unknown(self) -> None:
        with self.assertRaises(CHECKER.FreshnessUnknown):
            self.check(
                {
                    "status": "ahead",
                    "ahead_by": 1,
                    "behind_by": 0,
                    "commits": [{"sha": "123456789", "commit": None}],
                }
            )


class TransportFailureTests(unittest.TestCase):
    def test_transport_failures_return_unknown_exit_status(self) -> None:
        failures = (
            http.client.RemoteDisconnected("remote closed the connection"),
            http.client.IncompleteRead(b"partial", 1),
        )

        for failure in failures:
            with (
                self.subTest(failure=type(failure).__name__),
                mock.patch.object(CHECKER, "read_version_property", return_value="pin"),
                mock.patch.object(CHECKER.urllib.request, "urlopen", side_effect=failure),
                contextlib.redirect_stdout(io.StringIO()),
                contextlib.redirect_stderr(io.StringIO()),
            ):
                self.assertEqual(CHECKER.EXIT_UNKNOWN, CHECKER.main())


class MainTests(unittest.TestCase):
    @staticmethod
    def version(_name: str) -> str:
        return "pin"

    def run_main(self, branch_result: tuple[bool, list[str]]) -> tuple[int, str, str]:
        current_release = (True, ["- release current"])
        stdout = io.StringIO()
        stderr = io.StringIO()
        with (
            mock.patch.object(CHECKER, "read_version_property", side_effect=self.version),
            mock.patch.object(CHECKER, "check_release_pin", return_value=current_release),
            mock.patch.object(CHECKER, "check_branch_pin", return_value=branch_result),
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            status = CHECKER.main()
        return status, stdout.getvalue(), stderr.getvalue()

    def test_current_exit_status(self) -> None:
        status, output, error = self.run_main((True, ["- branch current"]))

        self.assertEqual(CHECKER.EXIT_CURRENT, status)
        self.assertIn("Every pin matches upstream", output)
        self.assertEqual("", error)

    def test_stale_has_dedicated_exit_status(self) -> None:
        status, output, error = self.run_main((False, ["- branch stale"]))

        self.assertEqual(CHECKER.EXIT_STALE, status)
        self.assertNotEqual(1, status)
        self.assertIn("One or more pins", output)
        self.assertEqual("", error)

    def test_unknown_exit_status(self) -> None:
        stdout = io.StringIO()
        stderr = io.StringIO()
        with (
            mock.patch.object(
                CHECKER, "read_version_property", side_effect=CHECKER.FreshnessUnknown("broken input")
            ),
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            status = CHECKER.main()

        self.assertEqual(CHECKER.EXIT_UNKNOWN, status)
        self.assertIn("Upstream pin freshness", stdout.getvalue())
        self.assertIn("freshness unknown: broken input", stderr.getvalue())

    def test_empty_commit_message_preserves_stale_exit_status(self) -> None:
        comparison = {
            "status": "ahead",
            "ahead_by": 1,
            "behind_by": 0,
            "commits": [{"sha": "123456789", "commit": {"message": ""}}],
        }
        stdout = io.StringIO()
        stderr = io.StringIO()
        with (
            mock.patch.object(CHECKER, "read_version_property", return_value="pin"),
            mock.patch.object(CHECKER, "check_release_pin", return_value=(True, ["- release current"])),
            mock.patch.object(CHECKER, "github_json", return_value=comparison),
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            status = CHECKER.main()

        self.assertEqual(CHECKER.EXIT_STALE, status)
        self.assertIn("(empty commit message)", stdout.getvalue())
        self.assertEqual("", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
