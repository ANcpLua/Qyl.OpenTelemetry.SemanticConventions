#!/usr/bin/env python3
"""Report when an upstream ref pinned in Version.props has fallen behind upstream.

The registry pins are exact by design — a moving registry would change generated
constants without a commit here. The cost is that nothing about a stale pin is
self-announcing, and one kind is entirely silent: SemConvGenAiRef is a bare commit
SHA on a branch-tracked upstream, so there is no "a newer version exists" signal to
notice. That is how the pin sat 9 commits behind and reached
gen_ai.request.previous_response.id (upstream #372) late.

This does not gate anything. A pin falling behind is news about upstream, not a
defect in this repository, so wiring it into ci.yml would redden unrelated pull
requests the moment upstream commits and teach everyone to ignore it. It runs on a
schedule and reports.

Three pins, two shapes:

  SemConvSchemaVersion  release tag   v{version} vs the latest release
  WeaverVersion         release tag   v{version} vs the latest release
  SemConvGenAiRef       branch SHA    commit distance from the tracked branch head

A lookup that cannot complete exits 2 rather than reporting "current". A check
unable to distinguish a fresh pin from an unreachable upstream is worse than no
check, because it reports green while blind.

CLI: check_pin_freshness.py   (exit 0 = every pin current; exit 1 = a pin is behind,
     reported on stdout; exit 2 = a lookup failed)
"""
from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

GITHUB_API = os.environ.get("GITHUB_API_URL", "https://api.github.com")
REPO_ROOT = Path(__file__).resolve().parents[3]
VERSION_PROPS = REPO_ROOT / "Version.props"

CORE_REPO = os.environ.get("SEMCONV_CORE_UPSTREAM", "open-telemetry/semantic-conventions")
GENAI_REPO = os.environ.get("SEMCONV_GENAI_UPSTREAM", "open-telemetry/semantic-conventions-genai")
GENAI_BRANCH = os.environ.get("SEMCONV_GENAI_BRANCH", "main")
WEAVER_REPO = os.environ.get("SEMCONV_WEAVER_UPSTREAM", "open-telemetry/weaver")

COMPARE_COMMIT_LIMIT = 10


class LookupFailed(Exception):
    """An upstream lookup could not be completed, so freshness is unknown."""


def read_version_property(name: str) -> str:
    """Read a pin from Version.props, honouring the same overrides as generate.sh."""
    override = {
        "SemConvSchemaVersion": "SEMCONV_SCHEMA_VERSION",
        "SemConvGenAiRef": "SEMCONV_GENAI_REF",
        "WeaverVersion": "SEMCONV_WEAVER_VERSION",
    }.get(name)
    if override and os.environ.get(override):
        return os.environ[override].strip()

    value = ET.parse(VERSION_PROPS).getroot().findtext(f".//{name}")
    if value is None or not value.strip():
        raise SystemExit(f"error: Version.props does not define {name}")
    return value.strip()


def github_json(path: str) -> dict:
    request = urllib.request.Request(f"{GITHUB_API}/{path}")
    request.add_header("Accept", "application/vnd.github+json")
    request.add_header("X-GitHub-Api-Version", "2022-11-28")
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token:
        request.add_header("Authorization", f"Bearer {token}")

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        detail = f"HTTP {error.code}"
        if error.code in (403, 429):
            detail += " (rate limited; set GITHUB_TOKEN)"
        elif error.code == 404:
            detail += " (renamed, deleted, or unknown ref)"
        raise LookupFailed(f"{path}: {detail}") from error
    except (urllib.error.URLError, TimeoutError) as error:
        raise LookupFailed(f"{path}: {error}") from error
    except json.JSONDecodeError as error:
        raise LookupFailed(f"{path}: upstream returned malformed JSON: {error}") from error


def check_release_pin(label: str, repo: str, pinned_version: str) -> tuple[bool, list[str]]:
    """Compare a pinned release version against the repository's latest release."""
    release = github_json(f"repos/{repo}/releases/latest")
    latest_tag = release.get("tag_name")
    if not latest_tag:
        raise LookupFailed(f"repos/{repo}/releases/latest: response carried no tag_name")

    pinned_tag = f"v{pinned_version}"
    if latest_tag == pinned_tag:
        return True, [f"- **{label}** current at `{pinned_tag}` ({repo})"]

    return False, [
        f"- **{label}** pinned at `{pinned_tag}`, latest release is `{latest_tag}`",
        f"  - {release.get('html_url', f'https://github.com/{repo}/releases')}",
    ]


def check_branch_pin(label: str, repo: str, pinned_sha: str, branch: str) -> tuple[bool, list[str]]:
    """Measure how far a pinned commit sits behind the head of a tracked branch."""
    comparison = github_json(f"repos/{repo}/compare/{pinned_sha}...{branch}")
    status = comparison.get("status")
    if status is None:
        raise LookupFailed(f"repos/{repo}/compare: response carried no status")

    # Compare is expressed from the base's perspective: base...head reports how far
    # head runs ahead of the pin, which is how far the pin trails the branch.
    behind_by = comparison.get("ahead_by", 0)
    if status == "identical" or behind_by == 0:
        return True, [f"- **{label}** current at `{pinned_sha[:7]}`, the head of `{branch}` ({repo})"]

    if status == "diverged":
        return False, [
            f"- **{label}** pinned at `{pinned_sha[:7]}`, which has **diverged** from `{branch}` "
            f"({behind_by} ahead on the branch, {comparison.get('behind_by', 0)} only on the pin)",
            f"  - {comparison.get('html_url', '')}",
        ]

    total = comparison.get("total_commits", behind_by)
    lines = [
        f"- **{label}** pinned at `{pinned_sha[:7]}`, **{behind_by} commit(s) behind** `{branch}` ({repo})",
        f"  - {comparison.get('html_url', '')}",
    ]

    commits = comparison.get("commits", [])
    for commit in commits[-COMPARE_COMMIT_LIMIT:]:
        subject = (commit.get("commit", {}).get("message") or "").splitlines()[0]
        lines.append(f"  - `{commit.get('sha', '')[:7]}` {subject}")
    if total > len(commits):
        lines.append(f"  - …{total - len(commits)} further commit(s) not listed by the compare API")

    return False, lines


def main() -> int:
    pins = {
        "SemConvSchemaVersion": read_version_property("SemConvSchemaVersion"),
        "SemConvGenAiRef": read_version_property("SemConvGenAiRef"),
        "WeaverVersion": read_version_property("WeaverVersion"),
    }

    report: list[str] = ["## Upstream pin freshness", ""]
    stale = False

    try:
        for current, lines in (
            check_release_pin("SemConvSchemaVersion", CORE_REPO, pins["SemConvSchemaVersion"]),
            check_branch_pin("SemConvGenAiRef", GENAI_REPO, pins["SemConvGenAiRef"], GENAI_BRANCH),
            check_release_pin("WeaverVersion", WEAVER_REPO, pins["WeaverVersion"]),
        ):
            stale = stale or not current
            report.extend(lines)
    except LookupFailed as error:
        print("\n".join(report), flush=True)
        print(f"\nfreshness unknown: {error}", file=sys.stderr)
        return 2

    report.append("")
    report.append(
        "One or more pins are behind upstream. Bumping is a deliberate change: re-run "
        "`scripts/generate.sh`, review the regenerated registry, and record the delta in "
        "`qyl-references/REFERENCE-STATUS.md`."
        if stale
        else "Every pin matches upstream."
    )
    print("\n".join(report))
    return 1 if stale else 0


if __name__ == "__main__":
    sys.exit(main())
